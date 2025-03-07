using KeyDerivation;
using System.Collections.Generic;

namespace Tuvi.Core
{
    public class ImplementationDetailsProvider : IBackupDetailsProvider, IKeyDerivationDetailsProvider
    {
        private string _keyDerivationSalt;
        private string _backupPackageIdentifier;

        private Dictionary<SpecialPgpKeyType, string> _specialPgpKeyIdentities;

        public ImplementationDetailsProvider(string keyDerivationSalt, string backupPackageIdentifier, string backupPgpKeyIdentity)
        {
            _keyDerivationSalt = keyDerivationSalt;
            _backupPackageIdentifier = backupPackageIdentifier;

            _specialPgpKeyIdentities = new Dictionary<SpecialPgpKeyType, string>
                                      {
                                          { SpecialPgpKeyType.Backup, backupPgpKeyIdentity }
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
