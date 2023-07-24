using Tuvi.Core.Backup;
using System.Collections.Generic;

namespace Tuvi.Core.Backup.Impl
{
    /// <summary>
    /// Common internal json backup data representation.
    /// </summary>
    /// <remarks>
    /// byte[] is a backup section content: json-serialized base64-encoded data.
    /// </remarks>
    internal class BackupSectionsDictionary : Dictionary<BackupSectionType, byte[]>
    { }

    /// <summary>
    /// Enumeration of logical backup sections.
    /// </summary>
    /// <remarks>
    /// Don't change existing element names.
    /// </remarks>
    public enum BackupSectionType : int
    {
        Version = 0,
        Time,
        Accounts,
        AddressBook,
        Messages,
        PublicKeys,
        Settings,
    }
}
