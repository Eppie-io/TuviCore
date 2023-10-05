using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.DataStorage
{
    public interface IPasswordProtectedStorage
    {
        /// <summary>
        /// Check if storage exist.
        /// </summary>
        Task<bool> IsStorageExistAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates data storage with specified <parameter name="password"/>.
        /// </summary>
        /// <exception cref="DataBaseAlreadyExistsException">Database is already created.</exception>
        Task CreateAsync(string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Open data storage with specified <parameter name="password"/>.
        /// </summary>
        /// <exception cref="DataBasePasswordException">On incorrect storage password provided.</exception>
        /// <exception cref="DataBaseNotExtistExistsException">Database is not created.</exception>
        /// <exception cref="DataBaseMigrationException">Database is failed to migrate to newer version.</exception>
        Task OpenAsync(string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Change storage password to <paramref name="newPassword"/>.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="DataBasePasswordException">On incorrect <paramref name="currentPassword"/> provided.</exception>
        Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reset storage to uninitialized state. All stored data is wiped.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task ResetAsync();
    }
}
