// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    class AccountFolder
    {
        private readonly IAccountService _accountService;
        public IAccountService AccountService => _accountService;
        private readonly Folder _folder;
        public Folder Folder => _folder;

        public AccountFolder(IAccountService accountService, Folder folder)
        {
            Debug.Assert(accountService != null);
            Debug.Assert(folder != null);
            _accountService = accountService;
            _folder = folder;
        }
    }

    public class CompositeFolder
    {
        private List<AccountFolder> _children;
        private IReadOnlyList<AccountFolder> Children => _children;
        public IReadOnlyList<Folder> Folders => Children.Select(x => x.Folder).ToList();
        public int UnreadCount { get; private set; }
        public string FullName { get; private set; }
        private List<Exception> _exceptions = new List<Exception>();
        public IEnumerable<Exception> Exceptions => _exceptions;

        internal CompositeFolder(IReadOnlyList<Folder> children, Func<Folder, IAccountService> mapper)
        {
            _children = children.Select(x => new AccountFolder(mapper(x), x)).ToList();
            UnreadCount = children.Select(x => x.UnreadCount).Sum();
            FullName = string.Join(";", children.Select(x => x.FullName).Distinct());
        }

        public bool HasSameName(CompositeFolder other)
        {
            if (other is null)
            {
                return false;
            }
            return HasSameName(other.FullName);
        }

        public bool HasSameName(string other)
        {
            return string.Equals(FullName, other, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<int> GetUnreadMessagesCountAsync(CancellationToken cancellationToken)
        {
            var tasks = Children.Select(x => x.AccountService.GetUnreadMessagesCountInFolderAsync(x.Folder, cancellationToken)).ToList();
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
            CollectExceptions(tasks);
            return tasks.Where(x => x.Status == TaskStatus.RanToCompletion).Select(x => x.Result).Sum();
        }

        public async Task<IReadOnlyList<Message>> ReceiveEarlierMessagesAsync(int count, CancellationToken cancellationToken = default)
        {
            var tasks = Children.Select(x => x.AccountService.ReceiveEarlierMessagesAsync(x.Folder, count, cancellationToken)).ToList();
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
            var list = tasks.ToList();
            CollectExceptions(tasks);
            return list.Where(x => x.Status == TaskStatus.RanToCompletion).SelectMany(x => x.Result).ToList();
        }

        public async Task<IReadOnlyList<Message>> ReceiveNewMessagesAsync(CancellationToken cancellationToken = default)
        {
            var tasks = Children.Select(async (x) =>
            {
                return await x.AccountService.ReceiveNewMessagesInFolderAsync(x.Folder, cancellationToken).ConfigureAwait(false);
            });
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
            CollectExceptions(tasks);
            return tasks.Where(x => x.Status == TaskStatus.RanToCompletion).SelectMany(x => x.Result).ToList();
        }
        public async Task UpdateFolderStructureAsync(CancellationToken cancellationToken = default)
        {
            var tasks = Children.Select(x => x.AccountService.UpdateFolderStructureAsync(cancellationToken));
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
            CollectExceptions(tasks);
            // TODO: update children
        }

        public async Task SynchronizeAsync(bool full, CancellationToken cancellationToken)
        {
            var tasks = Children.Select(x => x.AccountService.SynchronizeFolderAsync(x.Folder, full, cancellationToken));
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
            CollectExceptions(tasks);
        }

        private void CollectExceptions(IEnumerable<Task> tasks)
        {
            _exceptions.Clear();
            _exceptions.AddRange(tasks.Where(x => x.Status == TaskStatus.Faulted)
                                      .SelectMany(x => x.Exception.Flatten().InnerExceptions));
        }
    }

}
