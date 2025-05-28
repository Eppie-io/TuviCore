namespace Tuvi.Core.Entities
{
    public enum AuthenticationType
    {
        Unknown,
        Basic,
        OAuth2,
        Proton
    }

    public interface IAuthenticationData
    {
        AuthenticationType Type { get; }
    }

    public abstract class AuthenticationData : IAuthenticationData
    {
        public AuthenticationType Type { get; protected set; } = AuthenticationType.Unknown;

        protected static bool IsSame(object obj, object other)
        {
            return obj?.Equals(other) ?? other == null;
        }
    }

    public class BasicAuthData : AuthenticationData
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        public int AccountId { get; set; }

        public string Password { get; set; }

        // For outgoing server authentication if it differs
        public string OutgoingLogin { get; set; }
        public string OutgoingPassword { get; set; }

        // For incoming server authentication if it differs
        public string IncomingLogin { get; set; }
        public string IncomingPassword { get; set; }

        public BasicAuthData()
        {
            Type = AuthenticationType.Basic;
        }

        public override bool Equals(object obj)
        {
            return obj is BasicAuthData other 
                && IsSame(Password, other.Password)
                && IsSame(IncomingLogin, other.IncomingLogin)
                && IsSame(IncomingPassword, other.IncomingPassword)
                && IsSame(OutgoingLogin, other.OutgoingLogin)
                && IsSame(OutgoingPassword, other.OutgoingPassword);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class OAuth2Data : AuthenticationData
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        public int AccountId { get; set; }

        public string RefreshToken { get; set; }
        public string AuthAssistantId { get; set; } // ToDo: may be: rename it

        public OAuth2Data()
        {
            Type = AuthenticationType.OAuth2;
        }

        public override bool Equals(object obj)
        {
            return obj is OAuth2Data other &&
                IsSame(RefreshToken, other.RefreshToken) &&
                IsSame(AuthAssistantId, other.AuthAssistantId);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class ProtonAuthData : AuthenticationData
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        public int AccountId { get; set; }

        public string UserId { get; set; }

        public string RefreshToken { get; set; }

        public string SaltedPassword { get; set; }

        public ProtonAuthData()
        {
            Type = AuthenticationType.Proton;
        }

        public override bool Equals(object obj)
        {
            return obj is ProtonAuthData other &&
                IsSame(RefreshToken, other.RefreshToken) &&
                IsSame(UserId, other.UserId);
        }

        public override int GetHashCode()
        {
            return (UserId, RefreshToken).GetHashCode();
        }
    }
}
