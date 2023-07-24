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
            return tasks.Where(x => x.Status == TaskStatus.RanToCompletion).Select(x => x.Result).Sum();
        }

        public async Task<IReadOnlyList<Message>> ReceiveEarlierMessagesAsync(int count, CancellationToken cancellationToken = default)
        {
            var tasks = Children.Select(x => x.AccountService.ReceiveEarlierMessagesAsync(x.Folder, count, cancellationToken)).ToList();
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
            var list = tasks.ToList();
            return list.Where(x => x.Status == TaskStatus.RanToCompletion).SelectMany(x => x.Result).ToList();
        }

        public async Task<IReadOnlyList<Message>> ReceiveNewMessagesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var tasks = Children.Select(async (x) =>
                {
                    try
                    {
                        return await x.AccountService.ReceiveNewMessagesInFolderAsync(x.Folder, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw new NewMessagesCheckFailedException(new List<EmailFolderError>() { new EmailFolderError(x.Folder.AccountEmail, x.Folder, ex) });
                    }
                });
                await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
                return tasks.SelectMany(x => x.Result).ToList();
            }
            catch (AggregateException aex)
            {
                var errors = aex.InnerExceptions.OfType<NewMessagesCheckFailedException>().Select(x => x.ErrorsCollection).SelectMany(x => x).ToList();
                if (errors.Count == 0)
                {
                    throw;
                }
                // ignore all others if we have NewMessagesCheckFailedException
                throw new NewMessagesCheckFailedException(errors.ToList());
            }
        }
        public async Task UpdateFolderStructureAsync(CancellationToken cancellationToken = default)
        {
            var tasks = Children.Select(x => x.AccountService.UpdateFolderStructureAsync(cancellationToken));
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
            // TODO: update children
        }

        public async Task SynchronizeAsync(bool full, CancellationToken cancellationToken)
        {
            var tasks = Children.Select(x => x.AccountService.SynchronizeFolderAsync(x.Folder, full, cancellationToken));
            await tasks.DoWithLogAsync<CompositeFolder>().ConfigureAwait(false);
        }
    }

}
