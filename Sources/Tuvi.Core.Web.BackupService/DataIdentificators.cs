using System;

namespace Tuvi.Core.Web.BackupService
{
    public static class DataIdentificators
    {
        public static readonly string PublicKeyExtension = ".publickey";
        public static readonly string SignatureExtension = ".signature";
        public static readonly string BackupExtension = ".backup";
        public static readonly string CidExtension = ".cid";

        public static readonly string BackupPgpKeyIdentity = "backup@system.service.tuvi.com";
    }
}
