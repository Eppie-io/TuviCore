using System;

namespace Tuvi.Core.Entities
{
    /// <summary>
    /// Backup serialization protocol version representation.
    /// </summary>
    public class BackupProtocolVersion
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Build { get; set; }
        public int Revision { get; set; }

        public override bool Equals(object obj)
        {
            return obj is BackupProtocolVersion version &&
                   Major == version.Major &&
                   Minor == version.Minor &&
                   Build == version.Build &&
                   Revision == version.Revision;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(BackupProtocolVersion lhs, BackupProtocolVersion rhs)
        {
            if (Object.ReferenceEquals(lhs, null))
            {
                if (Object.ReferenceEquals(rhs, null))
                {
                    return true;
                }

                return false;
            }
            return lhs.Equals(rhs);
        }

        public static bool operator !=(BackupProtocolVersion lhs, BackupProtocolVersion rhs)
        {
            return !(lhs == rhs);
        }
    }
}
