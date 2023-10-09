using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// ToDo: Auth-old
//using Auth.Interfaces;
//using Auth.Interfaces.Types.Exceptions;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Impl.CredentialsManagement
{
    public static class CredentialsManagerCreator
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "<Pending>")]
        // ToDo: Auth-old
        public static ICredentialsManager GetCredentialsProvider(/*IAuthProvider authProvider*/)
        {
            return new CredentialsManager(/*authProvider*/);
        }
    }

    internal class CredentialsManager : ICredentialsManager, IDisposable
    {
        // ToDo: Auth-old
        //private readonly IAuthProvider _authProvider;
        //private readonly Dictionary<string, IAuthCredential> _cacheAuthCredentials = new Dictionary<string, IAuthCredential>();
        private readonly SemaphoreSlim _cacheSemaphore = new SemaphoreSlim(1);

        // ToDo: Auth-old
        //public CredentialsManager(IAuthProvider authProvider)
        //{
        //    _authProvider = authProvider;
        //}

        public ICredentialsProvider CreateCredentialsProvider(Account account)
        {
            // ToDo: Auth-old
            //try
            //{
            ICredentialsProvider provider = null;
            switch (account?.AuthData)
            {
                case BasicAuthData basicAuthData:

                    provider = new BasicCredentialsProvider()
                    {
                        BasicCredentials = new BasicCredentials()
                        {
                            UserName = account.Email.Address,
                            Password = basicAuthData.Password
                        }
                    };

                    break;
                case OAuth2Data oauth2Data:

                    provider = new OAuth2CredentialsProvider()
                    {
                        OAuth2Credentials = new OAuth2Credentials()
                        {
                            UserName = account.Email.Address,
                        },
                        // ToDo: Auth-old
                        TokenResolver = /*async*/ (CancellationToken ct) =>
                        {
                            //var authCredentials = await FindAuthCredentials(account.Email.Address, oauth2Data, ct).ConfigureAwait(false);
                            //var accessToken = await authCredentials.GetAccessTokenForRequestAsync(ct).ConfigureAwait(false);
                            //return accessToken;
                            return Task.FromResult("fake-token");
                        }
                    };

                    break;
            }

            return provider;
            // ToDo: Auth-old
            //}
            //catch (AuthProtocolErrorException e)
            //{
            //    throw new AuthenticationException(account.Email, e.Message, e);
            //}
        }

        public void Dispose()
        {
            _cacheSemaphore.Dispose();
        }

        // ToDo: Auth-old
        //private async Task<IAuthCredential> FindAuthCredentials(string userId, OAuth2Data oauth2Data, CancellationToken cancellationToken)
        //{
        //    await _cacheSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        //    try
        //    {
        //        if (_cacheAuthCredentials.TryGetValue(userId, out var cache))
        //        {
        //            return cache;
        //        }

        //        var id = oauth2Data.AuthAssistantId;
        //        var data = new OAuth2CredentialsProvider.AuthStoredData()
        //        {
        //            RefreshToken = oauth2Data.RefreshToken
        //        };

        //        var authCredentials = await _authProvider.GetAuthAssistant(id).RestoreAsync(data, cancellationToken).ConfigureAwait(false);
        //        _cacheAuthCredentials.Add(userId, authCredentials);

        //        return authCredentials;
        //    }
        //    finally
        //    {
        //        _cacheSemaphore.Release();
        //    }
        //}

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
            public OAuth2Credentials OAuth2Credentials { get; set; }
            public Func<CancellationToken, Task<string>> TokenResolver { get; set; }

            public async Task<AccountCredentials> GetCredentialsAsync(HashSet<string> supportedAuthMechanisms, CancellationToken cancellationToken = default)
            {
                if (TokenResolver != null)
                {
                    OAuth2Credentials.AccessToken = await TokenResolver(cancellationToken).ConfigureAwait(true);
                }

                return OAuth2Credentials;
            }

            // ToDo: Auth-old
            //internal class AuthStoredData : IAuthStoredData
            //{
            //    public string RefreshToken { get; set; }
            //}
        }
    }
}
