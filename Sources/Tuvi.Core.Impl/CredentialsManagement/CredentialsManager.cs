using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Impl.CredentialsManagement
{
    public static class CredentialsManagerCreator
    {
        public static ICredentialsManager GetCredentialsProvider(IDataStorage storage, ITokenRefresher tokenRefresher)
        {
            return new CredentialsManager(storage, tokenRefresher);
        }
    }

    internal class CredentialsManager : ICredentialsManager, IDisposable
    {
        private readonly TokenResolver _tokenResolver;
        private readonly IDataStorage _storage;
        private bool disposedValue;

        internal CredentialsManager(IDataStorage storage, ITokenRefresher tokenRefresher)
        {
            _tokenResolver = new TokenResolver(tokenRefresher);
            _storage = storage;
        }

        public ICredentialsProvider CreateCredentialsProvider(Account account)
        {
            ICredentialsProvider provider = null;
            switch (account?.AuthData)
            {
                case ProtonAuthData protonData:

                    provider = new ProtonCredentialsProvider()
                    {
                        Credentials = new ProtonCredentials()
                        {
                            UserName = account.Email.Address,
                            UserId = protonData.UserId,
                            RefreshToken = protonData.RefreshToken,
                            SaltedPassword = protonData.SaltedPassword
                        }
                    };
                    break;
            }

            return provider;
        }

        public ICredentialsProvider CreateIncomingCredentialsProvider(Account account)
        {
            ICredentialsProvider provider = null;
            switch (account?.AuthData)
            {
                case BasicAuthData basicAuthData:
                    {
                        BasicCredentials basicCredentials = null;

                        basicCredentials = CreateIncomingBasicCredentials(account, basicAuthData);

                        provider = new BasicCredentialsProvider()
                        {
                            BasicCredentials = basicCredentials
                        };
                        break;
                    }

                case OAuth2Data data:
                    {
                        provider = CreateOAuth2CredentialsProvider(account, data);

                        break;
                    }
            }

            return provider;
        }

        public ICredentialsProvider CreateOutgoingCredentialsProvider(Account account)
        {
            ICredentialsProvider provider = null;
            switch (account?.AuthData)
            {
                case BasicAuthData basicAuthData:
                    {
                        BasicCredentials basicCredentials = null;
                        basicCredentials = CreateOutgoingBasicCredentials(account, basicAuthData);

                        provider = new BasicCredentialsProvider()
                        {
                            BasicCredentials = basicCredentials
                        };
                        break;
                    }

                case OAuth2Data data:
                    {
                        provider = CreateOAuth2CredentialsProvider(account, data);

                        break;
                    }
            }

            return provider;
        }

        private static BasicCredentials CreateIncomingBasicCredentials(Account account, BasicAuthData basicAuthData)
        {
            BasicCredentials basicCredentials;
            if (string.IsNullOrEmpty(basicAuthData.IncomingLogin))
            {
                basicCredentials = new BasicCredentials()
                {
                    UserName = account.Email.Address,
                    Password = basicAuthData.Password
                };
            }
            else
            {
                basicCredentials = new BasicCredentials()
                {
                    UserName = basicAuthData.IncomingLogin,
                    Password = basicAuthData.IncomingPassword
                };
            }

            return basicCredentials;
        }

        private static BasicCredentials CreateOutgoingBasicCredentials(Account account, BasicAuthData basicAuthData)
        {
            BasicCredentials basicCredentials;
            if (string.IsNullOrEmpty(basicAuthData.OutgoingLogin))
            {
                basicCredentials = new BasicCredentials()
                {
                    UserName = account.Email.Address,
                    Password = basicAuthData.Password
                };
            }
            else
            {
                basicCredentials = new BasicCredentials()
                {
                    UserName = basicAuthData.OutgoingLogin,
                    Password = basicAuthData.OutgoingPassword
                };
            }

            return basicCredentials;
        }

        private ICredentialsProvider CreateOAuth2CredentialsProvider(Account account, OAuth2Data data)
        {
            ICredentialsProvider provider;
            _tokenResolver.AddOrUpdateToken(account.Email, data.AuthAssistantId, data.RefreshToken);

            provider = new OAuth2CredentialsProvider(_storage, account, data)
            {
                TokenResolver = (EmailAddress address, CancellationToken ct) => _tokenResolver.GetAccessTokenAsync(address, ct)
            };
            return provider;
        }

        internal class BasicCredentialsProvider : ICredentialsProvider
        {
            public BasicCredentials BasicCredentials { get; set; }

            public Task<AccountCredentials> GetCredentialsAsync(HashSet<string> supportedAuthMechanisms, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AccountCredentials>(BasicCredentials);
            }
        }

        internal class OAuth2CredentialsProvider : ICredentialsProvider
        {
            private readonly IDataStorage _storage;
            private readonly EmailAddress _emailAddress;

            private Account _newAccount;
            private string _refreshToken;

            internal Func<EmailAddress, CancellationToken, Task<(string accessToken, string newRefreshToken)>> TokenResolver { get; set; }

            public OAuth2CredentialsProvider(IDataStorage storage, Account newAccount, OAuth2Data data)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }

                _storage = storage ?? throw new ArgumentNullException(nameof(storage));
                _newAccount = newAccount ?? throw new ArgumentNullException(nameof(newAccount));
                _emailAddress = _newAccount.Email;
                _refreshToken = data.RefreshToken;
            }

            public async Task<AccountCredentials> GetCredentialsAsync(HashSet<string> supportedAuthMechanisms, CancellationToken cancellationToken = default)
            {
                return new OAuth2Credentials()
                {
                    UserName = _emailAddress.Address,
                    AccessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false)
                };
            }

            private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                (string accessToken, string refreshToken) = await TokenResolver(_emailAddress, cancellationToken).ConfigureAwait(false);

                string oldRefreshToken = _refreshToken;
                _refreshToken = refreshToken;

                if (_newAccount != null || !_refreshToken.Equals(oldRefreshToken, StringComparison.Ordinal))
                {
                    await UpdateAccount(_refreshToken, cancellationToken).ConfigureAwait(false);
                }

                return accessToken;
            }

            private async Task UpdateAccount(string refreshToken, CancellationToken cancellationToken = default)
            {
                try
                {
                    Account account = await _storage.GetAccountAsync(_emailAddress, cancellationToken).ConfigureAwait(false);
                    UpdateAccount(account, refreshToken);
                    await _storage.UpdateAccountAuthAsync(account, cancellationToken).ConfigureAwait(false);
                    _newAccount = null;
                }
                catch (Exception exception) when (exception is AccountIsNotExistInDatabaseException)
                {
                    // The account is not in storage at the time of creation
                    UpdateAccount(_newAccount, refreshToken);
                }
            }

            private void UpdateAccount(Account account, string refreshToken)
            {
                if (account?.AuthData is OAuth2Data oauth2Data)
                {
                    oauth2Data.RefreshToken = refreshToken;
                }
                else if (account != null)
                {
                    throw new AuthenticationException(account.Email, "Account doesn't have authentication data", null);
                }
                else
                {
                    throw new AuthenticationException(_emailAddress, "Account not found", null);
                }
            }
        }

        internal class ProtonCredentialsProvider : ICredentialsProvider
        {
            public ProtonCredentials Credentials { get; set; }

            public Task<AccountCredentials> GetCredentialsAsync(HashSet<string> supportedAuthMechanisms, CancellationToken cancellationToken = default)
            {
                return Task.FromResult((AccountCredentials)Credentials);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _tokenResolver.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~CredentialsManager()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
