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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;

namespace Tuvi.Core.Mail.Impl.Protocols
{
    abstract class MailService : IService
    {
        protected abstract MailKit.MailService Service { get; }

        public bool IsConnected => Service.IsConnected;

        public bool IsAuthenticated => Service.IsAuthenticated;

        private string ServerAddress { get; }
        private int ServerPort { get; }

        private ICredentialsProvider CredentialsProvider { get; set; }

        protected MailService(string serverAddress, int serverPort, ICredentialsProvider credentialsProvider)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentNullException(nameof(serverAddress));
            }
            if (serverPort < 0 || serverPort > 65535)
            {
                // ToDo:
                // if (serverPort < ushort.MinValue || ushort.MaxValue < serverPort)
                // We can throw ArgumentOutOfRangeException
                throw new ArgumentException($"{nameof(serverPort)} must be in [0,65535] range", nameof(serverPort));
            }
            if (credentialsProvider is null)
            {
                throw new ArgumentNullException(nameof(credentialsProvider));
            }
            ServerAddress = serverAddress;
            ServerPort = serverPort;
            CredentialsProvider = credentialsProvider;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            this.Log().LogDebug("ConnectAsync started");

            const int maxAttempts = 3;
            const int retryDelayMs = 2000;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await Service.ConnectAsync(ServerAddress, ServerPort, MailKit.Security.SecureSocketOptions.Auto, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (System.Net.Sockets.SocketException exp)
                {
                    if (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    throw new ConnectionException(exp.Message, exp);
                }
                catch (System.IO.IOException exp)
                {
                    var innerCanceled = exp.InnerException as TaskCanceledException;
                    if (innerCanceled != null)
                    {
                        throw innerCanceled;
                    }
                    if (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    throw new ConnectionException(exp.Message, exp);
                }
                catch (MailKit.Security.SslHandshakeException exp)
                {
                    if (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    throw new ConnectionException(exp.Message, exp);
                }
                catch (MailKit.ProtocolException exp)
                {
                    if (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    throw new ConnectionException(exp.Message, exp);
                }
            }
        }

        public async Task AuthenticateAsync(NetworkCredential credential, CancellationToken cancellationToken)
        {
            this.Log().LogDebug("AuthenticateAsync (with credential) started");

            try
            {
                await Service.AuthenticateAsync(credential, cancellationToken).ConfigureAwait(false);
            }
            catch (MailKit.Security.SaslException exp)
            {
                throw new AuthenticationException(exp.Message, exp);
            }
            catch (MailKit.Security.AuthenticationException exp)
            {
                throw new AuthenticationException(exp.Message, exp);
            }
            catch (System.IO.IOException exp)
            {
                throw new AuthenticationException(exp.Message, exp);
            }
            catch (MailKit.Net.Imap.ImapCommandException exp)
            {
                throw new AuthenticationException(exp.Message, exp);
            }
            catch (MailKit.ProtocolException exp)
            {
                throw new AuthenticationException(exp.Message, exp);
            }
        }

        public async Task AuthenticateAsync(CancellationToken cancellationToken)
        {
            this.Log().LogDebug("AuthenticateAsync started");

            var userName = string.Empty;

            try
            {
                var credentials = await CredentialsProvider.GetCredentialsAsync(Service.AuthenticationMechanisms, cancellationToken).ConfigureAwait(false);

                userName = credentials.UserName;

                switch (credentials)
                {
                    case BasicCredentials basicCredentials:
                        {
                            var networkCredentials = new NetworkCredential(userName, basicCredentials.Password);
                            await Service.AuthenticateAsync(networkCredentials, cancellationToken).ConfigureAwait(false);
                        }
                        break;
                    case OAuth2Credentials oauth2Credentials:
                        {
                            var oauth2 = new MailKit.Security.SaslMechanismOAuth2(userName, oauth2Credentials.AccessToken);
                            Service.AuthenticationMechanisms.Clear();
                            Service.AuthenticationMechanisms.Add(oauth2.MechanismName);
                            await Service.AuthenticateAsync(oauth2, cancellationToken).ConfigureAwait(false);
                        }
                        break;
                }
            }
            catch (MailKit.Security.SaslException exp)
            {
                throw new AuthenticationException(new EmailAddress(userName), exp.Message, exp);
            }
            catch (MailKit.Security.AuthenticationException exp)
            {
                throw new AuthenticationException(new EmailAddress(userName), exp.Message, exp);
            }
            catch (System.IO.IOException exp)
            {
                if (exp.InnerException is OperationCanceledException)
                {
                    throw exp.InnerException;
                }
                throw new AuthenticationException(new EmailAddress(userName), exp.Message, exp);
            }
            catch (MailKit.Net.Imap.ImapCommandException exp)
            {
                throw new AuthenticationException(new EmailAddress(userName), exp.Message, exp);
            }
            catch (MailKit.ProtocolException exp)
            {
                throw new AuthenticationException(new EmailAddress(userName), exp.Message, exp);
            }
        }

        public async Task DisconnectAsync()
        {
            await Service.DisconnectAsync(true).ConfigureAwait(false);
        }

        protected virtual Task ForceReconnectCoreAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected async Task RestoreConnectionAsync(CancellationToken cancellationToken)
        {
            this.Log().LogDebug("RestoreConnectionAsync started");

            bool needHardReconnect = false;
            try
            {
                if (!IsConnected)
                {
                    await ConnectAsync(cancellationToken).ConfigureAwait(false);
                }

                if (IsConnected && !IsAuthenticated)
                {
                    await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ConnectionException)
            {
                needHardReconnect = true;
            }
            catch (System.IO.IOException)
            {
                needHardReconnect = true;
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                needHardReconnect = true;
            }
            catch (MailKit.ProtocolException)
            {
                needHardReconnect = true;
            }

            if (needHardReconnect || !IsConnected || !IsAuthenticated)
            {
                await ForceReconnectCoreAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
