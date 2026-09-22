// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2026 Eppie (https://eppie.io)                                    //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Tuvi.Core.Mail.Impl.Protocols.IMAP;

namespace Tuvi.Core.Mail.Impl.Tests
{
    public class IMAPLoggerTests
    {
        private static Mock<ILogger> CreateSink(bool enabled = true)
        {
            var sink = new Mock<ILogger>();
            sink.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(enabled);
            return sink;
        }

        private static void AssertData(Mock<ILogger> sink, params string[] expected)
        {
            var entries = sink.Invocations.Where(x => x.Method.Name == nameof(ILogger.Log)).ToArray();
            Assert.That(entries, Has.Length.EqualTo(expected.Length));
            for (int i = 0; i < entries.Length; i++)
            {
                var args = entries[i].Arguments;
                Assert.That(args[0], Is.EqualTo(LogLevel.Trace));
                Assert.That(args[3], Is.Null);
                var state = (IEnumerable<KeyValuePair<string, object>>)args[2];
                Assert.That(state.ToArray(), Is.EqualTo(new[]
                {
                    new KeyValuePair<string, object>("Data", expected[i]),
                    new KeyValuePair<string, object>("{OriginalFormat}", "IMAP Client: {Data}")
                }));
                var formatter = (Delegate)args[4];
                Assert.That(formatter.DynamicInvoke(args[2], args[3]), Is.EqualTo("IMAP Client: " + expected[i]));
            }
        }

        [TestCase(0, false)]
        [TestCase(5, false)]
        [TestCase(0, true)]
        [TestCase(5, true)]
        public void MasksReportedRangesWithoutChangingNetworkBuffer(int prefixLength, bool multiple)
        {
            const string data = "A1 LOGIN user password\r\n";
            var buffer = Encoding.ASCII.GetBytes(new string('X', prefixLength) + data + "OUTSIDE");
            var original = (byte[])buffer.Clone();
            var detector = new Mock<IAuthenticationSecretDetector>(MockBehavior.Strict);
            var secrets = new List<AuthenticationSecret>();
            if (multiple)
            {
                secrets.Add(new AuthenticationSecret(prefixLength + 9, 4));
            }
            secrets.Add(new AuthenticationSecret(prefixLength + 14, 8));
            detector.Setup(x => x.DetectSecrets(buffer, prefixLength, data.Length)).Returns(secrets);
            var sink = CreateSink();
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };

            logger.LogClient(buffer, prefixLength, data.Length);

            AssertData(sink, multiple ? "A1 LOGIN ******** ********\r\n" : "A1 LOGIN user ********\r\n");
            Assert.That(buffer, Is.EqualTo(original));
            detector.Verify(x => x.DetectSecrets(buffer, prefixLength, data.Length), Times.Once);
            detector.VerifyNoOtherCalls();
        }

        [Test]
        public void ProcessesEachLineAndTrailingFragmentOnceBeforePublishingOneEntry()
        {
            var buffer = Encoding.ASCII.GetBytes("XXfirst\r\nsecond\nlastYY");
            var detector = new Mock<IAuthenticationSecretDetector>(MockBehavior.Strict);
            var sequence = new MockSequence();
            detector.InSequence(sequence).Setup(x => x.DetectSecrets(buffer, 2, 7)).Returns(Array.Empty<AuthenticationSecret>());
            detector.InSequence(sequence).Setup(x => x.DetectSecrets(buffer, 9, 7))
                .Returns(new[] { new AuthenticationSecret(9, 6) });
            detector.InSequence(sequence).Setup(x => x.DetectSecrets(buffer, 16, 4)).Returns(Array.Empty<AuthenticationSecret>());
            var sink = CreateSink();
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };

            logger.LogClient(buffer, 2, 18);

            AssertData(sink, "first\r\n********\nlast");
            detector.VerifyAll();
            Assert.That(detector.Invocations, Has.Count.EqualTo(3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ProcessesFragmentsInOrderEvenWhenTraceIsInitiallyDisabled(bool initiallyEnabled)
        {
            var first = Encoding.ASCII.GetBytes("secret-part-one\r");
            var second = Encoding.ASCII.GetBytes("\nsecret-part-two");
            var detector = new Mock<IAuthenticationSecretDetector>(MockBehavior.Strict);
            var sequence = new MockSequence();
            detector.InSequence(sequence).Setup(x => x.DetectSecrets(first, 0, first.Length))
                .Returns(new[] { new AuthenticationSecret(0, first.Length - 1) });
            detector.InSequence(sequence).Setup(x => x.DetectSecrets(second, 0, 1)).Returns(Array.Empty<AuthenticationSecret>());
            detector.InSequence(sequence).Setup(x => x.DetectSecrets(second, 1, second.Length - 1))
                .Returns(new[] { new AuthenticationSecret(1, second.Length - 1) });
            var sink = CreateSink(initiallyEnabled);
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };

            logger.LogClient(first, 0, first.Length);
            if (!initiallyEnabled)
            {
                AssertData(sink);
            }
            sink.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true);
            logger.LogClient(second, 0, second.Length);

            AssertData(sink, initiallyEnabled ? new[] { "********\r", "\n********" } : new[] { "\n********" });
            detector.VerifyAll();
            Assert.That(detector.Invocations, Has.Count.EqualTo(3));
        }

        [Test]
        public void SamplesTraceSettingOncePerCallback()
        {
            var buffer = Encoding.ASCII.GetBytes("NOOP\r\n");
            var sink = CreateSink();
            var detector = new Mock<IAuthenticationSecretDetector>();
            detector.Setup(x => x.DetectSecrets(buffer, 0, buffer.Length))
                .Callback(() => sink.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(false))
                .Returns(Array.Empty<AuthenticationSecret>());
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };

            logger.LogClient(buffer, 0, buffer.Length);

            AssertData(sink, "NOOP\r\n");
            sink.Verify(x => x.IsEnabled(LogLevel.Trace), Times.Once);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DetectorFailureSuppressesEntireCallbackAndAllFutureClientData(bool traceEnabled)
        {
            var buffer = Encoding.ASCII.GetBytes("first\r\nsecret\r\n");
            var detector = new Mock<IAuthenticationSecretDetector>(MockBehavior.Strict);
            detector.Setup(x => x.DetectSecrets(buffer, 0, 7)).Returns(Array.Empty<AuthenticationSecret>());
            detector.Setup(x => x.DetectSecrets(buffer, 7, 8)).Throws(new InvalidOperationException("secret"));
            var sink = CreateSink(traceEnabled);
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };

            logger.LogClient(buffer, 0, buffer.Length);
            sink.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true);
            logger.LogClient(buffer, 0, buffer.Length);
            logger.AuthenticationSecretDetector = Mock.Of<IAuthenticationSecretDetector>();
            logger.LogClient(buffer, 0, buffer.Length);

            Assert.That(sink.Invocations.All(x => x.Method.Name != nameof(ILogger.Log)), Is.True);
            detector.VerifyAll();
            Assert.That(detector.Invocations, Has.Count.EqualTo(2));
        }

        [TestCase(-1, 1)]
        [TestCase(0, -1)]
        [TestCase(0, int.MaxValue)]
        [TestCase(7, 0)]
        public void InvalidSecretRangeSuppressesClientData(int start, int length)
        {
            var buffer = Encoding.ASCII.GetBytes("secret");
            var detector = new Mock<IAuthenticationSecretDetector>();
            detector.Setup(x => x.DetectSecrets(buffer, 0, buffer.Length))
                .Returns(new[] { new AuthenticationSecret(start, length) });
            var sink = CreateSink();
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };

            logger.LogClient(buffer, 0, buffer.Length);
            logger.LogClient(buffer, 0, buffer.Length);

            AssertData(sink);
            detector.Verify(x => x.DetectSecrets(buffer, 0, buffer.Length), Times.Once);
        }

        [Test]
        public void MissingDetectorDoesNotWriteClientData()
        {
            var sink = CreateSink();
            using var logger = new IMAPLogger(sink.Object);
            var buffer = Encoding.ASCII.GetBytes("secret");
            logger.LogClient(buffer, 0, buffer.Length);
            AssertData(sink);
        }

        [Test]
        public void EmptySliceDoesNotCallDetectorOrWriteData()
        {
            var detector = new Mock<IAuthenticationSecretDetector>(MockBehavior.Strict);
            var sink = CreateSink();
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };
            var buffer = Encoding.ASCII.GetBytes("unused");
            logger.LogClient(buffer, buffer.Length, 0);
            AssertData(sink);
            detector.VerifyNoOtherCalls();
        }

        [TestCase(-1, 0)]
        [TestCase(7, 0)]
        [TestCase(0, -1)]
        [TestCase(1, int.MaxValue)]
        public void RejectsInvalidSliceBeforeCallingDetector(int offset, int count)
        {
            var detector = new Mock<IAuthenticationSecretDetector>(MockBehavior.Strict);
            var sink = CreateSink();
            using var logger = new IMAPLogger(sink.Object) { AuthenticationSecretDetector = detector.Object };
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => logger.LogClient(new byte[6], offset, count)));
            AssertData(sink);
            detector.VerifyNoOtherCalls();
        }

        [Test]
        public void RejectsNullBuffer()
        {
            using var logger = new IMAPLogger(CreateSink().Object);
            Assert.Throws<ArgumentNullException>((Action)(() => logger.LogClient(null, 0, 0)));
        }

        [Test]
        public void ImapClientInstallsAuthenticationDetectorWithoutConnecting()
        {
            using var logger = new IMAPLogger(CreateSink().Object);
            Assert.That(logger.AuthenticationSecretDetector, Is.Null);
            using var client = new ImapClient(logger);
            Assert.That(logger.AuthenticationSecretDetector, Is.Not.Null);
        }
    }
}
