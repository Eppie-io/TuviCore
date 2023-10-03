using KeyDerivation;
using System.Collections.Generic;

namespace Tuvi.Core
{
    public class ImplementationDetailsProvider : IBackupDetailsProvider, IKeyDerivationDetailsProvider
    {
        // TODO: Needs to be moved to the client side
        public static string BackupPgpKeyIdentity => "backup@system.service.tuvi.com";

        private string _keyDerivationSalt;
        private string _backupPackageIdentifier;
        
        private Dictionary<SpecialPgpKeyType, string> _specialPgpKeyIdentities;

        public ImplementationDetailsProvider(string keyDerivationSalt, string backupPackageIdentifier)
        {
            _keyDerivationSalt = keyDerivationSalt;
            _backupPackageIdentifier = backupPackageIdentifier;

            _specialPgpKeyIdentities = new Dictionary<SpecialPgpKeyType, string>
                                      {
                                          { SpecialPgpKeyType.Backup, BackupPgpKeyIdentity }
                                      };
        }        

        public string GetPackageIdentifier()
        {
            return _backupPackageIdentifier;
        }

        public string GetSaltPhrase()
        {
            return _keyDerivationSalt;
        }

        public int GetSeedPhraseLength()
        {
            return 12;
        }

        public Dictionary<SpecialPgpKeyType, string> GetSpecialPgpKeyIdentities()
        {
            return _specialPgpKeyIdentities;
        }
    }
}
