namespace Tuvi.Core
{
    public interface IBackupDetailsProvider
    {
        /// <summary>
        /// Get package identifier to be used with backup implementations
        /// </summary>
        string GetPackageIdentifier();
    }
}
