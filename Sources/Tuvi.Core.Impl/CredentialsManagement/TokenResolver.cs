// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Tuvi.Core.Entities;

namespace Tuvi.Core.Impl.CredentialsManagement
{
    internal class TokenResolver : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private static readonly TimeSpan ReserveTime = TimeSpan.FromMinutes(1);

        private readonly ITokenRefresher _refresher;

        private readonly ConcurrentDictionary<EmailAddress, TokenData> _tokenStore = new ConcurrentDictionary<EmailAddress, TokenData>();
        private bool _disposedValue;

        public TokenResolver(ITokenRefresher refresher)
        {
            _refresher = refresher;
        }

        public void AddOrUpdateToken(EmailAddress emailAddress, string mailService, string refreshToken)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            if (string.IsNullOrWhiteSpace(mailService))
            {
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", nameof(mailService));
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", nameof(refreshToken));
            }

            TokenData data = CreateTokenData(mailService, refreshToken);

            _tokenStore.AddOrUpdate(
                key: emailAddress,
                addValue: data,
                updateValueFactory: (key, oldData) => oldData.Token.RefreshToken.Equals(data.Token.RefreshToken, StringComparison.Ordinal) ? oldData : data);
        }

        public async Task<(string accessToken, string refreshToken)> GetAccessTokenAsync(EmailAddress emailAddress, CancellationToken cancellationToken = default)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            TokenData data = FindData(emailAddress);
            AuthToken result = data.Token;

            if (data.ExpireTime < DateTime.UtcNow + ReserveTime)
            {
                result = await RefreshAsync(emailAddress, cancellationToken).ConfigureAwait(false);
            }

            return (result.AccessToken, result.RefreshToken);
        }

        private async Task<AuthToken> RefreshAsync(EmailAddress emailAddress, CancellationToken cancellationToken = default)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                TokenData data = FindData(emailAddress);

                if (data.ExpireTime < DateTime.UtcNow + ReserveTime)
                {
                    AuthToken token = data.Token;
                    AuthToken freshToken = await _refresher.RefreshTokenAsync(data.MailService, token.RefreshToken, cancellationToken).ConfigureAwait(false);
                    token.Update(freshToken);

                    TokenData newData = CreateTokenData(data.MailService, token);

                    _tokenStore.AddOrUpdate(emailAddress, newData, (key, oldData) => newData);

                    return token;
                }

                return data.Token;
            }
            catch (AuthorizationException e)
            {
                throw new AuthorizationException(emailAddress, e.InnerException.Message, e.InnerException);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private TokenData FindData(EmailAddress emailAddress)
        {
            return _tokenStore.TryGetValue(emailAddress, out TokenData data)
                ? data
                : throw new AuthenticationException(emailAddress, "The token is not stored", null);
        }

        private static TokenData CreateTokenData(string mailService, AuthToken token)
        {
            return new TokenData()
            {
                MailService = mailService,
                ExpireTime = DateTime.UtcNow + token.ExpiresIn,
                Token = token
            };
        }

        private static TokenData CreateTokenData(string mailService, string refreshToken)
        {
            return new TokenData()
            {
                MailService = mailService,
                ExpireTime = DateTime.MinValue,
                Token = new AuthToken()
                {
                    AccessToken = string.Empty,
                    RefreshToken = refreshToken,
                    ExpiresIn = TimeSpan.Zero
                }
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _semaphore.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null

                _tokenStore.Clear();
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected struct TokenData
        {
            public string MailService { get; set; }
            public DateTime ExpireTime { get; set; }
            public AuthToken Token { get; set; }
        }
    }
}
