using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl.Protocols
{
    abstract class MailService : IService
    {
        protected abstract MailKit.MailService Service { get; }

        public bool IsConnected => Service.IsConnected;

        public bool IsAuthentificated => Service.IsAuthenticated;

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
            try
            {
                await Service.ConnectAsync(ServerAddress, ServerPort, MailKit.Security.SecureSocketOptions.Auto, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Net.Sockets.SocketException exp)
            {
                throw new ConnectionException(exp.Message, exp);
            }
            catch (System.IO.IOException exp)
            {
                var innerCanceled = exp.InnerException as TaskCanceledException;
                if (innerCanceled != null)
                {
                    throw innerCanceled;
                }
                throw new ConnectionException(exp.Message, exp);
            }
            catch (MailKit.ProtocolException exp)
            {
                throw new ConnectionException(exp.Message, exp);
            }
        }

        public async Task AuthenticateAsync(NetworkCredential credential, CancellationToken cancellationToken)
        {
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
            catch (MailKit.ProtocolException exp)
            {
                throw new AuthenticationException(exp.Message, exp);
            }
        }

        public async Task AuthenticateAsync(CancellationToken cancellationToken)
        {
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
            catch (MailKit.ProtocolException exp)
            {
                throw new AuthenticationException(new EmailAddress(userName), exp.Message, exp);
            }
        }

        public async Task DisconnectAsync()
        {
            await Service.DisconnectAsync(true).ConfigureAwait(false);
        }

        protected async Task RestoreConnectionAsync(CancellationToken cancellationToken)
        {
            if (!IsConnected)
            {
                await ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            if (IsConnected && !IsAuthentificated)
            {
                await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
