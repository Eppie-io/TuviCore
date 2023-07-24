using System.Collections.Generic;
using System.Linq;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public class CompositeAccount
    {
        private IReadOnlyList<CompositeFolder> _foldersStructure;
        public IReadOnlyList<CompositeFolder> FoldersStructure => _foldersStructure;
        public EmailAddress Email => _addresses.First();
        private List<EmailAddress> _addresses;
        public IReadOnlyList<EmailAddress> Addresses => _addresses;

        private CompositeFolder _defaultInboxFolder;
        public CompositeFolder DefaultInboxFolder => _defaultInboxFolder;

        internal CompositeAccount(IReadOnlyList<CompositeFolder> folders, IEnumerable<EmailAddress> emailAddresses, CompositeFolder inboxFolder)
        {
            _foldersStructure = folders;
            _defaultInboxFolder = inboxFolder;
            _addresses = new List<EmailAddress>(emailAddresses);
        }
    }
}
