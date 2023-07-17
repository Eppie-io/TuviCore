using System.Collections.Generic;

namespace Tuvi.Core.Entities
{
    public class FolderMessagesBackupContainer
    {
        public string FolderFullName { get; set; }
        public IReadOnlyList<Message> Messages { get; set; }

        public FolderMessagesBackupContainer(string folderFullName, IReadOnlyList<Message> messages)
        {
            FolderFullName = folderFullName;
            Messages = messages;
        }
    }

    public class EmailAccountBackupContainer
    {
        public string EmailAccount { get; set; }
        public IReadOnlyList<FolderMessagesBackupContainer> Folders { get; set; }

        public EmailAccountBackupContainer(string emailAccount, IReadOnlyList<FolderMessagesBackupContainer> folders)
        {
            EmailAccount = emailAccount;
            Folders = folders;
        }
    }
}
