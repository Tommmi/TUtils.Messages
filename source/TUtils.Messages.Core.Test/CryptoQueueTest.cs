using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Common.Logging.LogMocs;
using TUtils.Common.Security.Asymmetric;
using TUtils.Common.Security.Asymmetric.Common;
using TUtils.Common.Security.Asymmetric.RSACryptoServiceProvider;
using TUtils.Common.Security.Symmetric.AesCryptoServiceProvider;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Core.Common;
using TUtils.Messages.Core.Queue;
using TUtils.Messages.Core.Queue.Messages;

namespace TUtils.Messages.Core.Test
{
	[TestClass]
	public class CryptoQueueTest
	{
        //[TestMethod]
        //public async Task TestCryptoQueue1()
        //{
        //	var env = new ClientStandardEnvironment(
        //		clientUri: "gerlach-it.de/client1",
        //		additionalConfiguration: null,
        //		requestRetryIntervallTimeMs: 1000,
        //		rootAssemblies: Assembly.GetAssembly(GetType()));
        //	var certificateVerifier = new CertificateVerifier(env.SystemTime);
        //	var certificateProvider = new CertificateProvider() as ICertificateProvider;
        //	var cer1 = certificateProvider.GetPublicCertificateFromWindowsStorage(subjectName: "CER_1");
        //	var cer1Base64 = cer1.GetPrivateCertificateFromWindowsStorage().ToBase64String().Content;
        //	Assert.IsTrue(cer1 != null);
        //	var cer2 = certificateProvider.GetPublicCertificateFromWindowsStorage(subjectName: "CER_2");
        //	Assert.IsTrue(cer2 != null);

        //	await TestCryptQueueInternal(env, certificateVerifier, cer1, cer2);
        //}

        [TestInitialize]
        public void Initialize()
        {
            this.InitializeConsoleLogging(LogSeverityEnum.INFO);
        }

		[TestMethod]
		public async Task TestCryptoQueue2()
		{
			var env = new ClientStandardEnvironment(
				clientUri: "gerlach-it.de/client1",
				additionalConfiguration: null,
				diconnectedRetryIntervallTimeMs: 1000,
				rootAssemblies: Assembly.GetAssembly(GetType()));

			var certificateVerifier = Substitute.For<ICertificateVerifier>();
			certificateVerifier.IsValidAndTrusted(Arg.Any<ICertificate>()).Returns(new VerifyResult(verified:true,errorText:null));

			var certificateProvider = new CertificateProvider() as ICertificateProvider;
			var cer1 = certificateProvider.CreatePrivateCertificateDefinitionByString(
				"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")
				.GetPrivateCertificate();
			var cer2 = certificateProvider.CreatePrivateCertificateDefinitionByString(
				"MIIJtQIBAzCCCXUGCSqGSIb3DQEHAaCCCWYEggliMIIJXjCCBgkGCSqGSIb3DQEHAaCCBfoEggX2MIIF8jCCBe4GCyqGSIb" +
				"3DQEMCgECoIIE/jCCBPowHAYKKoZIhvcNAQwBAzAOBAiGvlV4/x7HtgICB9AEggTYgj7OsF7YGiyIkVuFDoLQfNmnUrgZCK" +
				"t+UonqKp3165w/B+3aXUikgQO9NthtT6CX5mqh5CRxeQoi3Kt+hmlTvNPR/k7HaHu4aynZKZLAsV8hHDRJmdwKtsWijo9QB" +
				"E8ZOm54mvtlYIWXr0ErbNpkatB7COUaP3Gmy0eaOXG9l82k0mSDzblRpFAlFJbGuAcyzgswb5+keNh/u+sCTkUx5gB8i1dP" +
				"jRl0hQy6+JPXhzmY+oCl75afn5HNB4alek30BwER+NajKzVnXQ1cd4yQaeANs/GgMYB80I8CUeXrkb7iCWiacL6LbBdmrEu" +
				"auWRFDSiP/soYaITTl/jQcQ2YofCdmXAckxtyLZ4Vt+2Vbz3vKbmQadvSGRTpf62i6gjBEl54Onfb3nQgXBFhlixKZDdhek" +
				"2RTznWiqvTgfYbJXtCEWPPJ38ueRpuff+18SdhMgyfr0fogNuy4uXXso8ET96Fjf975uewnBnQC1qOS2WFUfEadwQ2D0brs" +
				"nr3VsBjDh5YeY94vABSmbWHF4DX2X7hKqG1MHAju0C67n1OZFQu9RB4sH846PMIqUIU5PNYuWModX0SS8T0p5CQjv8ZsNsJ" +
				"ZDC9jlSf8DSyrUnYGxtN2r1g30CnwKMbzVcMFZgKuYFk6rJSiddLa5KdsNjvA7HTB3j/nCUX6D4AX8UXYXGemCz7CaL8cPJ" +
				"nY4N/qjUIxgHJkPbtyHW3gD1wYFXBFc9Ry8Pl6g85NV4Kb2ojxe4te1rqDPytPfaQh6YB5BrXCLxzG6nFk9k4WFQkba8mZg" +
				"TLk7spY/Z62IZ4Mb76oGmBr0EoLn2sTLj+VWTf6AD4GfHDjwp6SAABlQfT+FoqkxKIrAkKvp8ceQ2gCX96HC/WHgr89Ef4k" +
				"d/X7BuUT9q+S2D8WeOx1rqAUn0q2QyxLyNAhLln6p5Ash8CrKFDe3z7ISbjG0d22YazxJ26kqXV4hg1YhK+LQ3WPxkFDm6J" +
				"PtdSrR+Zw4nTbhMrikaZeuDFWPBptdDdOTZ4N2WAKPxwpMQe6BllqtZmwadCJdkzcmssFSExv9G7n4miIrWOyfcUE/M6GOV" +
				"6LHaQAxvpz0cH8udOZ6/fDodps7xxhTPQg0ve7cOijQoX0L3sv0ZA90UaRcI9cfkQXuNFXK28ATU9nSxbEI9JM5A81axvfx" +
				"TMTnjRUtYOMfSub53sLtNayLK3UCJmCNQeOEoMChKRMKsAmgdQAkvFsATkNf1Jgg3bJc3GxXrZ/1NliXMqg5Glr3Cl70HZH" +
				"4M8bIizJuRNT0chp5cAdqKS4BI7YxGXf5OaVXDRp0rlho/JybQ+7wDEWOKDHhLnCrVNlxaTdbPz0HtmtLmH6ANXnYPFNcEG" +
				"ZjCWTjchVyLVFHC37k1pOHNKwmjovZkn+nShyPx19/bLZBkeEEmh2B3wIaEdDFX8bVx3OaCcgEf8Crb9Z9KzpPF3KYn86xR" +
				"mjRr6ZRNCPNzTQHzfJ/lW3rgxgMJKl51JLK5duxg+P0ejWwPhzjjXeWkWMSZ56A5hcDV1pu8UyCL1AGXNF420ZCabRXw9aX" +
				"31LjNe+JiLxgMMw/s9luBHeBpImGQYYkxbgGp0nCDcK324LhUz/LjiIPXfL6JmKpIBVv0W/GMKdEesOOy77JMrbO4gZgHE5" +
				"1ApyKuCJTGB3DANBgkrBgEEAYI3EQIxADATBgkqhkiG9w0BCRUxBgQEAQAAADBXBgkqhkiG9w0BCRQxSh5IADgAMgAzAGIA" +
				"MQBmADgAZQAtAGIAMABhAGYALQA0AGUAOQBkAC0AOQAyADYAMAAtADUANAA4ADYAYwAxAGMAMQA4ADUAZAA5MF0GCSsGAQQ" +
				"BgjcRATFQHk4ATQBpAGMAcgBvAHMAbwBmAHQAIABTAHQAcgBvAG4AZwAgAEMAcgB5AHAAdABvAGcAcgBhAHAAaABpAGMAIA" +
				"BQAHIAbwB2AGkAZABlAHIwggNNBgkqhkiG9w0BBwGgggM+BIIDOjCCAzYwggMyBgsqhkiG9w0BDAoBA6CCAwowggMGBgoqh" +
				"kiG9w0BCRYBoIIC9gSCAvIwggLuMIIB1qADAgECAhDDqKVs/zg+skKj7p+Gq1U9MA0GCSqGSIb3DQEBCwUAMA0xCzAJBgNV" +
				"BAMTAkNBMB4XDTE2MDYxNzEyMDU0OVoXDTM5MTIzMTIzNTk1OVowFTETMBEGA1UEAx4KAEMARQBSAF8AMjCCASIwDQYJKoZ" +
				"IhvcNAQEBBQADggEPADCCAQoCggEBAMwFRnXaBJv+ZE5QqAJV/d0Pk9lhvtICSc2qvd6eJEOcuoVaZB/mKOW5mJCi9gALM/" +
				"B9wUIBYrw+2bJFFXqsvNpTy8YOrD+nxqMCfvSTLuivYG1BbYwkMAjFUIL2/jAES+1dwfEPwXp3CwryxlMx6mYIwU3kuQ3id" +
				"VM5oMa2cWJcEXq2HzNccylelt/ECmeQy5Cz7a/vkIPs8dtDgsxxc1fvKM7Wg6tZLvt3Fei5fjDJir7P4u9edvB7IcCXEjMu" +
				"jUXOca7Mq0MNUbEZxNmVe3M5bcRL7vA/+dw0ChvW4r99Z1MDODK600zzrjwZEkb707wF1ujnu/HvQ/GF6+NHAPECAwEAAaN" +
				"CMEAwPgYDVR0BBDcwNYAQWiLOrPRtNtwys3qLCFl04qEPMA0xCzAJBgNVBAMTAkNBghA8SDbyZzQevEA6VbEbB6gzMA0GCS" +
				"qGSIb3DQEBCwUAA4IBAQBqLte1x8FURmTmkvECWD/DxGrkVo5p94X3sXAHEXkJ9qJL+iiinaUytFoL6dvrKdw/kHsE5N9t5" +
				"fk6uaQtDqcVvYSoKwygBhDhPSkkYQLQuaN7KCK5JaxXkk5ZxZpFyr2BJROYrSO62BDXF6YcZlbMCf+xMNmBs9O8/2n5WnP3" +
				"HITo7Nj3IysOyKTo72DdQzQRPV2pE5/iSxusjcsPO976f88FmWHMkfK8oxbC486WFU6y9zstRBuDDS+XfaFaxgm3aVI4SXr" +
				"2q/HLvJvffx2azJ/XINBcAZQeVAhBXzDN9Oy+FpRaitLVwSEUW+bx8oJLSfDCUxfaDJPvDQQiQrF+MRUwEwYJKoZIhvcNAQ" +
				"kVMQYEBAEAAAAwNzAfMAcGBSsOAwIaBBTL3ctfCY5o9BdWhSjqRxoVgOqdbAQUFVMcMdm02JAa4IdCdu2J9b6ZqGY=")
				.GetPrivateCertificate();

			await TestCryptQueueInternal(env, certificateVerifier, cer1, cer2);
		}

		private static async Task TestCryptQueueInternal(
			ClientStandardEnvironment env, 
			ICertificateVerifier certificateVerifier,
			ICertificate cer1, 
			ICertificate cer2)
		{
			var cryptProtocol = new CryptProtocol();
			var queueLeft = env.QueueFactory.Create();
			var queueRight = env.QueueFactory.Create();
			var symmetricCryptProvider = new SymmetricCryptProvider();

			var queueTailLeft = new CryptoQueueAdapter(
				queueFactory: env.QueueFactory,
				queueEntry: queueRight.Entry,
				queueExit: queueLeft.Exit,
				cryptProtocol: cryptProtocol,
				serializer: env.Serializer,
				certificateVerifier: certificateVerifier,
				certificate: cer1,
				symmetricCryptProvider: symmetricCryptProvider,
				cancellationToken: env.CancellationToken,
				timeoutMs: 4000) as IQueueTail;

			var queueTailRight = new CryptoQueueAdapter(
				queueFactory: env.QueueFactory,
				queueEntry: queueLeft.Entry,
				queueExit: queueRight.Exit,
				cryptProtocol: cryptProtocol,
				serializer: env.Serializer,
				certificateVerifier: certificateVerifier,
				certificate: cer2,
				symmetricCryptProvider: symmetricCryptProvider,
				cancellationToken: env.CancellationToken,
				timeoutMs: 4000) as IQueueTail;

			var testMessage = new TestRequestMessage(new Address("1234"), value: "hello");
			var enqueueTask = queueTailLeft.Enqueue(testMessage);
			await Task.WhenAny(enqueueTask, Task.Delay(2000, env.CancellationToken));
			Assert.IsTrue(enqueueTask.IsCompleted);
			var request = await queueTailRight.Dequeue(1000);
			Assert.IsTrue(!request.TimeoutElapsed);
			var requestMsg = request.Value as TestRequestMessage;
			Assert.IsTrue(requestMsg != null);
			Assert.IsTrue(requestMsg.Value == "hello");

			var respMessage = new TestResponseMessage(requestMsg);
			enqueueTask = queueTailRight.Enqueue(respMessage);
			await Task.WhenAny(enqueueTask, Task.Delay(2000, env.CancellationToken));
			Assert.IsTrue(enqueueTask.IsCompleted);
			var response = await queueTailLeft.Dequeue(1000);
			Assert.IsTrue(!response.TimeoutElapsed);
			var responseMsg = response.Value as TestResponseMessage;
			Assert.IsTrue(responseMsg != null);
			Assert.IsTrue(responseMsg.Value == "hello");
		}
	}
}
