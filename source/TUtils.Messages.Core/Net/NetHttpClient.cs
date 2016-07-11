using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common.Logging;
using TUtils.Messages.Common.Net;

namespace TUtils.Messages.Core.Net
{
	public class NetHttpClient : INetClient
	{
		public const string NetNodeAddressHttpHeaderValueName = "netNodeAddress";
		#region fields

		private readonly Uri _baseUri;
		private readonly INetNodeAddress _thisClientNodeAddress;
		private readonly CancellationToken _cancellationToken;
		private readonly ITLog _logger;
		private HttpClient _httpClient;

		#endregion

		#region constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="baseUri"></param>
		/// <param name="thisClientNodeAddress"></param>
		/// <param name="additionalConfiguration">may be null</param>
		/// <param name="cancellationToken"></param>
		/// <param name="logger"></param>
		public NetHttpClient(
			Uri baseUri, 
			INetNodeAddress thisClientNodeAddress,
			Action<HttpClient> additionalConfiguration, 
			CancellationToken cancellationToken,
			ITLog logger)
		{
			_baseUri = baseUri;
			_thisClientNodeAddress = thisClientNodeAddress;
			_cancellationToken = cancellationToken;
			_logger = logger;
			Start(additionalConfiguration);
		}

		#endregion

		#region private Members

		private void Start(Action<HttpClient> additionalConfiguration)
		{
			Dispose();
			_httpClient = new HttpClient {BaseAddress = _baseUri};
			_httpClient.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));
			_httpClient.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/octet-stream"));
			_httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
			additionalConfiguration?.Invoke(_httpClient);
		}

		private async Task<NetActionResultEnum> Enqueue(HttpRequestMessage request)
		{
			request.Headers.Add(NetNodeAddressHttpHeaderValueName, _thisClientNodeAddress.GetAsString());
			try
			{
				using (var responseMsg = await _httpClient.SendAsync(request, _cancellationToken))
				{
					if (!responseMsg.IsSuccessStatusCode)
						return GetFailureCode(responseMsg);

					return NetActionResultEnum.Succeeded;
				}
			}
			catch (Exception e)
			{
				_logger.LogException(e);
				return NetActionResultEnum.Exception;
			}
		}

		private static NetActionResultEnum GetFailureCode(HttpResponseMessage responseMsg)
		{
			switch (responseMsg.StatusCode)
			{
				case HttpStatusCode.OK:
					return NetActionResultEnum.Succeeded;
				case HttpStatusCode.NonAuthoritativeInformation:
				case HttpStatusCode.ProxyAuthenticationRequired:
					return NetActionResultEnum.AccessRejectedSecurity;
				case HttpStatusCode.RequestTimeout:
				case HttpStatusCode.GatewayTimeout:
					return NetActionResultEnum.Timeout;
				case HttpStatusCode.BadGateway:
					return NetActionResultEnum.NodeNotAvailable;
				default:
					return NetActionResultEnum.AccessRejected;
			}
		}

		private static async Task<MessageContent> GetMessageContent(HttpResponseMessage response)
		{
			MessageContent messageContent;
			var contentType = response.Content.Headers.FirstOrDefault(headerVal => headerVal.Key == "Content-Type").Value?.FirstOrDefault();
			if (contentType == "application/octet-stream")
				messageContent = new ByteMessageContent(await response.Content.ReadAsByteArrayAsync());
			else
				messageContent = new StringMessageContent(await response.Content.ReadAsStringAsync());
			return messageContent;
		}

		#endregion

		#region INetClient

		async Task<NetActionResultEnum> INetClient.Enqueue(byte[] data)
		{
			using (var request = new HttpRequestMessage(HttpMethod.Post, "enqueue"))
			{
				request.Content = new ByteArrayContent(data);
				request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				return await Enqueue(request);
			}
		}

		async Task<NetActionResultEnum> INetClient.Enqueue(string data)
		{
			using (var request = new HttpRequestMessage(HttpMethod.Post,"enqueue"))
			{
				request.Content = new StringContent(data);
				return await Enqueue(request);
			}
		}

		async Task<NetClientDequeueResult> INetClient.Dequeue()
		{
			using (var request = new HttpRequestMessage(HttpMethod.Get, "dequeue"))
			{
				request.Headers.Add(NetNodeAddressHttpHeaderValueName, _thisClientNodeAddress.GetAsString());
				var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					return new NetClientDequeueResult(GetFailureCode(response), null);
				}

				var content = await GetMessageContent(response);
				return new NetClientDequeueResult(NetActionResultEnum.Succeeded, content);
			}
		}

		#endregion

		#region IDisposable

		public void Dispose()
		{
			_httpClient?.Dispose();
			_httpClient = null;
		}

		#endregion
	}
}
