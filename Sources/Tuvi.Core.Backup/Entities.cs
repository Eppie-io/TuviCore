using System;

namespace Tuvi.Core.Backup
{
    /// <summary>
    /// Enumeration of package header versions.
    /// </summary>
    /// <remarks>
    /// Used in binary protocol.
    /// Don't change existing elements and order.
    /// </remarks>
    public enum PackageHeaderVersion : Int32
    {
        V1
    }

    /// <summary>
    /// Enumeration of possible data protection formats.
    /// </summary>
    /// <remarks>
    /// Used in binary protocol.
    /// Don't change existing elements and order.
    /// </remarks>
    public enum DataProtectionFormat : Int32
    {
        PgpEncryptionWithSignature = 0
    }

    /// <summary>
    /// Enumeration of possible content serialization formats.
    /// </summary>
    /// <remarks>
    /// Used in binary protocol.
    /// Don't change existing elements and order.
    /// </remarks>
    public enum ContentSerializationFormat : Int32
    {
        JsonUtf8 = 0
    }
}
