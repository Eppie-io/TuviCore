// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2026 Eppie (https://eppie.io)                                    //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

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
