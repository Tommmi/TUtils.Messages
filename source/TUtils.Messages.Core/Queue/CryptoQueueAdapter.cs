using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Common.Security.Asymmetric.Common;
using TUtils.Common.Security.Symmetric;
using TUtils.Common.Security.Symmetric.Common;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Common.Queue.messages;
using TUtils.Messages.Core.Queue.Messages;

namespace TUtils.Messages.Core.Queue
{
	// ReSharper disable once UnusedMember.Global
	public class CryptoQueueAdapter : QueueAdapterBase
	{
		private class CryptSession
		{
			public Guid SessionId { get; }
			public ISymmetricCrypt SymmetricCrypt { get; }

			public CryptSession(
				Guid sessionId,
				ISymmetricCrypt symmetricCrypt)
			{
				SessionId = sessionId;
				SymmetricCrypt = symmetricCrypt;
			}
		}
		private readonly ICryptProtocol _cryptProtocol;
		private readonly IMessageSerializer _serializer;
		private readonly ICertificateVerifier _certificateVerifier;
		private readonly IPrivateCertificate _privateCertificate;
		private readonly IPublicCertificate _publicCertificate;
		private readonly ISymmetricCryptProvider _symmetricCryptProvider;
		private readonly IQueue _sendingQueueBuffer;
		private readonly int _timeoutMs;
		private readonly object _sync = new object();
		private readonly List<CryptSession> _knownSessions = new List<CryptSession>();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="logger"></param>
		/// <param name="queueFactory"></param>
		/// <param name="queueEntry"></param>
		/// <param name="queueExit"></param>
		/// <param name="cryptProtocol"></param>
		/// <param name="serializer"></param>
		/// <param name="certificateVerifier"></param>
		/// <param name="certificate">
		/// may be a public certificate, but windows certification storage must have the private key !
		/// </param>
		/// <param name="symmetricCryptProvider"></param>
		/// <param name="cancellationToken"></param>
		/// <param name="timeoutMs"></param>
		public CryptoQueueAdapter(
			ITLog logger, 
			IQueueFactory queueFactory, 
			IQueueEntry queueEntry, 
			IQueueExit queueExit, 
			ICryptProtocol cryptProtocol,
			IMessageSerializer serializer,
			ICertificateVerifier certificateVerifier,
			ICertificate certificate,
			ISymmetricCryptProvider symmetricCryptProvider,
			CancellationToken cancellationToken, 
			int timeoutMs) : base(logger, queueFactory, queueEntry, queueExit, cancellationToken)
		{
			_cryptProtocol = cryptProtocol;
			_serializer = serializer;
			_certificateVerifier = certificateVerifier;
			_privateCertificate = certificate as IPrivateCertificate;
			_publicCertificate = certificate as IPublicCertificate;
			if (_publicCertificate == null)
			{
				if ( _privateCertificate == null )
					throw new ApplicationException("6753gqwz7dg2387 you must specifie a certificate");
				_publicCertificate = _privateCertificate.ToPublicCertificate();
			}
			else
			{
				_privateCertificate = _publicCertificate.GetPrivateCertificateFromWindowsStorage();
				if (_privateCertificate == null)
					throw new ApplicationException("896487fj2394 you must have a private certificate");
			}
			_sendingQueueBuffer = queueFactory.Create();
			_symmetricCryptProvider = symmetricCryptProvider;
			_timeoutMs = timeoutMs;
		}

		protected override async Task DequeueHook(object msg)
		{
			if (msg is ICryptedMessage)
			{
				var encryptedMessage = (ICryptedMessage)msg;

				// on ICryptedMessage
				var sessionId = encryptedMessage.SymmetricCryptSessionId;
				CryptSession cryptSession;
				lock (_sync)
				{
					cryptSession = _knownSessions.FirstOrDefault(s => s.SessionId == sessionId);
				}
				if (cryptSession == null)
					return;
				var encryptedData = encryptedMessage.Data;
				var serializedMessage = cryptSession.SymmetricCrypt.Decrypt(encryptedData).Data;
				var decryptedMsg = _serializer.Deserialize(new ByteMessageContent(serializedMessage));
				await ProceedDequeue(decryptedMsg);
			}
			else if (msg is IInitCryptographic)
			{
				await OnInitCryptMessage((IInitCryptographic) msg);
			}
		}

		protected override async Task EnqueueHook(object msg)
		{
			await _sendingQueueBuffer.Entry.Enqueue(msg);
			var connectionEstablished = await EnsureSecureConnection();
			object message;
			do
			{
				message = _sendingQueueBuffer.Exit.Peek();
				if (message != null && connectionEstablished)
				{
					var encryptedMsg = Encrypt(message);
					if (encryptedMsg != null )
						await ProceedEnqueue(encryptedMsg);
				}
			} while (message != null);
		}

		private object Encrypt(object message)
		{
			CryptSession cryptSession;
			lock (_sync)
			{
				cryptSession = _knownSessions.FirstOrDefault();
			}
			if (cryptSession == null)
				return null;
			var serializedMsgBytes = _serializer.Serialize(message).GetData();
			var encryptedMessageBytes = cryptSession.SymmetricCrypt.Encrypt(new PlainData(serializedMsgBytes));
			return _cryptProtocol.CreateEncryptedMessage(
				symmetricCryptSessionId:cryptSession.SessionId,
				data: encryptedMessageBytes);
		}

		private async Task<bool> EnsureSecureConnection()
		{
			lock (_sync)
			{
				if (_knownSessions.Any())
					return true;
			}

			var newAssymetricSessionId = Guid.NewGuid();
			var waitOnResponseTask = WaitOnReceivingMessage<InitCryptographicResponse>(
				filter: r => ((IInitCryptographicResponse)r).AssymetricCryptSessionId == newAssymetricSessionId,
				timeoutMs: _timeoutMs);
			await ProceedEnqueue(_cryptProtocol.CreateInitCryptographic(
				assymetricCryptSessionId: newAssymetricSessionId,
				publicCertifikate: _publicCertificate.ToBase64String()));
			var waitResult = await waitOnResponseTask;
			if (waitResult.TimeoutElapsed || waitResult.Value == null)
			{
				Logger.Log(LogSeverityEnum.ERROR, this,$"(7634723sj) other side didn't send response message InitCryptographicResponse");
				return false;
			}
			var response = (IInitCryptographicResponse) waitResult.Value;
			var encryptedSymmetricSecret = response.EncryptedSymmetricSecret;
			var symmetricSessionId = response.SymmetricCryptSessionId;
			var signature = response.Signature;
			var publicCertOfResponse = response.ClientCertifikate.GetPublicCertificate(null);
			if (!publicCertOfResponse.Verify(data: encryptedSymmetricSecret, signature: signature))
			{
				Logger.Log(LogSeverityEnum.ERROR, this, $"(8djg821hsh) InitCryptographicResponse hasn't signed the transfered secret");
				return false;
			}
			var isOtherNodeTrusted = _certificateVerifier.IsValidAndTrusted(publicCertOfResponse);
			if (!isOtherNodeTrusted.Verified)
			{
				Logger.Log(LogSeverityEnum.ERROR, this,$"(j873ehjalkxnvt) certificate of other side could be verified {isOtherNodeTrusted.ErrorText}");
				return false;
			}
			var serializedSecret = _privateCertificate.Decrypt(encryptedSymmetricSecret);
			var symmetricSecret = _serializer.Deserialize(new ByteMessageContent(serializedSecret)) as SymmetricSecret;
			if (symmetricSecret != null)
			{
				lock (_sync)
				{
					_knownSessions.Add(new CryptSession(symmetricSessionId, _symmetricCryptProvider.Create(symmetricSecret)));
				}
				return true;
			}
			Logger.Log(LogSeverityEnum.ERROR, this, $"(j823fdgakm) secret couldn't be deserialized");
			return false;
		}

		private async Task OnInitCryptMessage(IInitCryptographic msg)
		{
			var symmetricSessionId = Guid.NewGuid();
			var symmetricCrypt = _symmetricCryptProvider.Create();
			var serializedSymmetricSecret = _serializer.Serialize(symmetricCrypt.Secret);
			lock (_sync)
			{
				_knownSessions.Add(new CryptSession(symmetricSessionId, symmetricCrypt));
			}
			var publicCertificateOfSender = msg.PublicCertifikate.GetPublicCertificate(password:null);
			var verifyResult = _certificateVerifier.IsValidAndTrusted(publicCertificateOfSender);
			if (!verifyResult.Verified)
			{
				Logger.Log(LogSeverityEnum.ERROR, this,$"23gd273hn49t {verifyResult.ErrorText}");
				return;
			}
			var serializedSymmetricSecretBytes = serializedSymmetricSecret.GetData();
			var encryptedSymmetricSecret = publicCertificateOfSender.Encrypt(serializedSymmetricSecretBytes);
			// ReSharper disable once PossibleNullReferenceException
			var signature = _privateCertificate.Sign(encryptedSymmetricSecret);
			await ProceedEnqueue(_cryptProtocol.CreateInitCryptographicResponse(
				msg, symmetricSessionId, encryptedSymmetricSecret, _publicCertificate.ToBase64String(), signature));
		}
	}
}
