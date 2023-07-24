namespace Tuvi.Core.Backup
{
    /// <summary>
    /// Factory interface to create mutual backup data package builders and parsers.
    /// </summary>
    public interface IBackupSerializationFactory
    {
        /// <summary>
        /// Set backup packages identifier which later created builders and parsers will use.
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        void SetPackageIdentifier(string packageIdentifier);

        /// <summary>
        /// Used to build backup package. One builder per one backup package.
        /// </summary>
        IBackupBuilder CreateBackupBuilder();

        /// <summary>
        /// Used to parse backup package. One parser per one backup package.
        /// </summary>
        IBackupParser CreateBackupParser();
    }
}
