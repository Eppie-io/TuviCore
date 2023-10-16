using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Entities
{
    public class AccountCredentials
    {
        public string UserName { get; set; }
    }

    public class BasicCredentials : AccountCredentials
    {
        public string Password { get; set; }

        public static BasicCredentials Create(Account account)
        {
            if (account?.AuthData is BasicCredentials basicCredentials)
            {
                return new BasicCredentials()
                {
                    UserName = account.Email.Address,
                    Password = basicCredentials.Password
                };
            }

            return null;
        }
    }

    public class OAuth2Credentials : AccountCredentials
    {
        public string AccessToken { get; set; }
    }

    public class ProtonCredentials : AccountCredentials
    {
        public string UserId { get; set; }
        public string RefreshToken { get; set; }
        public string SaltedPassword { get; set; }
    }

    public interface ICredentialsProvider
    {
        Task<AccountCredentials> GetCredentialsAsync(HashSet<string> supportedAuthMechanisms, CancellationToken cancellationToken = default);
    }
}
