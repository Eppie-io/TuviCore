using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;
using Tuvi.Core.Mail.Impl.Protocols;

namespace Tuvi.Core.Mail.Impl
{
    class SemaphoreWithPriority : IDisposable
    {
        SemaphoreSlim _normalPriorityLock;
        SemaphoreSlim _highPriorityLock;
        private bool _disposedValue;
        private int _priorityWaiting;
        private object _lockObj;

        public SemaphoreWithPriority()
        {
            _lockObj = new object();
            _normalPriorityLock = new SemaphoreSlim(1);
            _highPriorityLock = new SemaphoreSlim(1);
        }

        public async Task WaitAsync(bool highPriority, CancellationToken cancellationToken)
        {
            if (highPriority)
            {
                lock (_lockObj)
                {
                    _priorityWaiting++;
                }
                await _highPriorityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                await _normalPriorityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            while (true)
            {
                await _normalPriorityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                lock (_lockObj)
                {
                    if (_priorityWaiting == 0)
                    {
                        return;
                    }
                }
                _normalPriorityLock.Release();
            }
        }

        public void Release(bool highPriority)
        {
            if (highPriority)
            {
                lock (_lockObj)
                {
                    _priorityWaiting--;
                    if (_priorityWaiting == 0)
                    {
                        _normalPriorityLock.Release();
                    }
                }
                _highPriorityLock.Release();
                return;
            }
            _normalPriorityLock.Release();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _normalPriorityLock.Dispose();
                    _highPriorityLock.Dispose();
                }

                _disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
    internal static class SynchronData
    {
        public static ConcurrentDictionary<string, SemaphoreWithPriority> ServiceSemaphore = new ConcurrentDictionary<string, SemaphoreWithPriority>();

        public static ConcurrentDictionary<string, ConcurrentQueue<object>> Requests = new ConcurrentDictionary<string, ConcurrentQueue<object>>();
    }

    internal abstract class Command<T>
    {
        protected abstract MailService Service { get; }

        protected virtual string GetUniqueCommandIdentifier(string _)
        {
            return null;
        }

        protected virtual bool IsHighPriority { get => false; }

        public async Task<T> RunCommand(string email, ICredentialsProvider credentialsProvider, CancellationToken cancellationToken)
        {
            //TODO: get rid of ICredentialsProvider
            var requestUniqueeID = GetUniqueCommandIdentifier(email);

            ConcurrentQueue<object> tcsList = null;
            if (!String.IsNullOrEmpty(requestUniqueeID))
            {
                // track commands where similar requests should have the same result
                do
                {
                    if (SynchronData.Requests.TryGetValue(requestUniqueeID, out tcsList))
                    {
                        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                        tcsList.Enqueue(tcs);
                        return await tcs.Task.ConfigureAwait(false);
                    }
                    if (tcsList is null)
                    {
                        tcsList = new ConcurrentQueue<object>();
                    }
                }
                while (!SynchronData.Requests.TryAdd(requestUniqueeID, tcsList));
            }

            string serviceName = Service.GetType().Name + email;
            var serviceSemaphore = SynchronData.ServiceSemaphore.GetOrAdd(serviceName, _ => new SemaphoreWithPriority());

            try
            {
                await serviceSemaphore.WaitAsync(IsHighPriority, cancellationToken).ConfigureAwait(true);
                try
                {
                    await PrepareToUse(credentialsProvider, cancellationToken).ConfigureAwait(true);
                    var res = await Execute(cancellationToken).ConfigureAwait(true);
                    if (!String.IsNullOrEmpty(requestUniqueeID))
                    {
                        // delete first, don't move to to finally
                        SynchronData.Requests.TryRemove(requestUniqueeID, out _);
                        while (tcsList.TryDequeue(out object tcs))
                        {
                            (tcs as TaskCompletionSource<T>).SetResult(res);
                        }
                    }
                    return res;
                }
                catch (IOException ex)
                {
                    if (ex.InnerException is OperationCanceledException)
                    {
                        // upwrap inner exception
                        throw ex.InnerException;
                    }
                    this.Log().LogError(ex, "An error occurred while executing the command");
                    throw;
                }
                finally
                {
                    serviceSemaphore.Release(IsHighPriority);
                }
            }
            catch (Exception ex)
            {
                if (!String.IsNullOrEmpty(requestUniqueeID))
                {
                    // delete first, don't move to to finally
                    SynchronData.Requests.TryRemove(requestUniqueeID, out _);
                    while (tcsList.TryDequeue(out object tcs))
                    {
                        (tcs as TaskCompletionSource<T>).SetException(ex);
                    }
                }
                throw;
            }
        }

        private async Task PrepareToUse(ICredentialsProvider credentialsProvider, CancellationToken cancellationToken)
        {
            if (!Service.IsConnected)
            {
                await Service.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!Service.IsAuthentificated)
            {
                await Service.AuthenticateAsync(credentialsProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        protected abstract Task<T> Execute(CancellationToken cancellationToken);
    }

    internal class SendCommand : Command<string>
    {
        private SenderService Sender;
        private Message Message;
        protected override MailService Service { get { return Sender; } }

        public SendCommand(SenderService sender, Message msg)
        {
            Sender = sender;
            Message = msg;
        }

        protected override async Task<string> Execute(CancellationToken cancellationToken)
        {
            Message.Date = DateTime.Now;
            var result = await Sender.SendMessageAsync(Message, cancellationToken).ConfigureAwait(false);
            return result;
        }
    }

    internal abstract class ReceiverCommand<T> : Command<T>
    {
        protected ReceiverService Receiver;
        protected Folder FolderPath;
        protected override MailService Service { get { return Receiver; } }

        public ReceiverCommand(ReceiverService receiver, Folder folder)
        {
            Receiver = receiver;
            FolderPath = folder;
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return null;
        }
    }

    internal class AppendSentMessageCommand : ReceiverCommand<bool>
    {
        string MessageID;
        Message Message;

        public AppendSentMessageCommand(ReceiverService receiver, Folder folder, Message msg, string messageID)
            : base(receiver, folder)
        {
            Message = msg;
            MessageID = messageID;
        }

        protected override async Task<bool> Execute(CancellationToken cancellationToken)
        {
            await Receiver.AppendSentMessageAsync(Message, MessageID, cancellationToken).ConfigureAwait(false);
            return true;
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath?.FullName + MessageID;
        }
    }

    internal class GetFoldersStructureCommand : ReceiverCommand<IList<Folder>>
    {
        public GetFoldersStructureCommand(ReceiverService receiver)
            : base(receiver, null)
        {
        }

        protected override async Task<IList<Folder>> Execute(CancellationToken cancellationToken)
        {
            return await Receiver.GetFoldersStructureAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal class GetDefaultInboxFolder : ReceiverCommand<Folder>
    {
        public GetDefaultInboxFolder(ReceiverService receiver)
            : base(receiver, null)
        {
        }

        protected async override Task<Folder> Execute(CancellationToken cancellationToken)
        {
            return await Task.Run(Receiver.GetDefaultInboxFolder, cancellationToken).ConfigureAwait(false);
        }
    }

    internal class GetMessagesCommand : ReceiverCommand<IReadOnlyList<Message>>
    {
        private int Count;
        public GetMessagesCommand(ReceiverService receiver, Folder folder, int count)
            : base(receiver, folder)
        {
            Count = count;
        }

        protected override Task<IReadOnlyList<Message>> Execute(CancellationToken cancellationToken)
        {
            return Receiver.GetMessagesAsync(FolderPath, Count, cancellationToken);
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName + Count.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal class GetMessageByIDCommand : ReceiverCommand<Message>
    {
        private uint ID;

        protected override bool IsHighPriority { get => true; }

        public GetMessageByIDCommand(ReceiverService receiver, Folder folder, uint id)
          : base(receiver, folder)
        {
            ID = id;
        }

        protected override Task<Message> Execute(CancellationToken cancellationToken)
        {
            return Receiver.GetMessageAsync(FolderPath, ID, cancellationToken);
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName + ID.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal class GetLaterMessagesCommand : ReceiverCommand<IReadOnlyList<Message>>
    {
        private Message LastMessage;
        private int Count;

        public GetLaterMessagesCommand(ReceiverService receiver, Folder folder, int count, Message lastMessage)
            : base(receiver, folder)
        {
            LastMessage = lastMessage;
            Count = count;
        }

        protected override Task<IReadOnlyList<Message>> Execute(CancellationToken cancellationToken)
        {
            return Receiver.GetLaterMessagesAsync(FolderPath, Count, LastMessage, cancellationToken);
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName + Count.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal class GetEarlierMessagesCommand : ReceiverCommand<IReadOnlyList<Message>>
    {
        private Message LastMessage;
        private int Count;
        private bool Fast;

        public GetEarlierMessagesCommand(ReceiverService receiver, Folder folder, int count, Message lastMessage, bool fast)
            : base(receiver, folder)
        {
            LastMessage = lastMessage;
            Count = count;
            Fast = fast;
        }

        protected override Task<IReadOnlyList<Message>> Execute(CancellationToken cancellationToken)
        {
            return Receiver.GetEarlierMessagesAsync(FolderPath, Count, LastMessage, Fast, cancellationToken);
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            var sb = new StringBuilder();
            sb.Append(this.GetType().Name)
                .Append(email)
                .Append(FolderPath.FullName)
                .Append(Count)
                .Append(Fast);
            if (LastMessage != null)
            {
                sb.Append(LastMessage.Id);
            }
            return sb.ToString();
        }
    }

    internal class MarkMessagesAsReadCommand : ReceiverCommand<bool>
    {
        private IList<uint> UIDs;

        public MarkMessagesAsReadCommand(ReceiverService receiver, Folder folder, IList<uint> uids)
            : base(receiver, folder)
        {
            UIDs = uids;
        }

        protected async override Task<bool> Execute(CancellationToken cancellationToken)
        {
            await Receiver.MarkMessagesAsReadAsync(UIDs, FolderPath, cancellationToken).ConfigureAwait(false);
            return true;
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName;
        }
    }

    internal class MarkMessagesAsUnReadCommand : ReceiverCommand<bool>
    {
        private IList<uint> UIDs;

        public MarkMessagesAsUnReadCommand(ReceiverService receiver, Folder folder, IList<uint> uids)
            : base(receiver, folder)
        {
            UIDs = uids;
        }

        protected async override Task<bool> Execute(CancellationToken cancellationToken)
        {
            await Receiver.MarkMessagesAsUnReadAsync(UIDs, FolderPath, cancellationToken).ConfigureAwait(false);
            return true;
        }
        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName;
        }

    }

    internal class DeleteMessagesCommand : ReceiverCommand<bool>
    {
        IReadOnlyList<uint> UIDS;
        bool PermanentDelete;

        public DeleteMessagesCommand(ReceiverService receiver, IReadOnlyList<uint> ids, Folder folder, bool permanentDelete)
            : base(receiver, folder)
        {
            UIDS = ids;
            PermanentDelete = permanentDelete;
        }

        protected async override Task<bool> Execute(CancellationToken cancellationToken)
        {
            await Receiver.DeleteMessagesAsync(UIDS, FolderPath, PermanentDelete, cancellationToken).ConfigureAwait(false);
            return true;
        }
        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name
                + email
                + FolderPath.FullName
                + String.Join(",", new List<uint>(UIDS).ConvertAll(x => x.ToString(CultureInfo.InvariantCulture)));
        }
    }


    internal class MoveMessagesCommand : ReceiverCommand<bool>
    {
        IReadOnlyList<uint> UIDS;
        Folder TargetFolder;

        public MoveMessagesCommand(ReceiverService receiver, IReadOnlyList<uint> ids, Folder folder, Folder targetFolder)
            : base(receiver, folder)
        {
            UIDS = ids;
            TargetFolder = targetFolder;
        }

        protected async override Task<bool> Execute(CancellationToken cancellationToken)
        {
            await Receiver.MoveMessagesAsync(UIDS, FolderPath, TargetFolder, cancellationToken).ConfigureAwait(false);
            return true;
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name
                + email
                + FolderPath.FullName
                + String.Join(",", new List<uint>(UIDS).ConvertAll(x => x.ToString(CultureInfo.InvariantCulture)));
        }
    }

    internal class FlagMessagesCommand : ReceiverCommand<bool>
    {
        private IList<uint> UIDs;

        public FlagMessagesCommand(ReceiverService receiver, Folder folder, IList<uint> uids)
            : base(receiver, folder)
        {
            UIDs = uids;
        }

        protected async override Task<bool> Execute(CancellationToken cancellationToken)
        {
            await Receiver.FlagMessagesAsync(UIDs, FolderPath, cancellationToken).ConfigureAwait(false);
            return true;
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName;
        }
    }

    internal class UnflagMessagesCommand : ReceiverCommand<bool>
    {
        private IList<uint> UIDs;

        public UnflagMessagesCommand(ReceiverService receiver, Folder folder, IList<uint> uids)
            : base(receiver, folder)
        {
            UIDs = uids;
        }

        protected async override Task<bool> Execute(CancellationToken cancellationToken)
        {
            await Receiver.UnflagMessagesAsync(UIDs, FolderPath, cancellationToken).ConfigureAwait(false);
            return true;
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName;
        }
    }

    internal class AppendDraftMessageCommand : ReceiverCommand<Message>
    {
        private Message Message;

        public AppendDraftMessageCommand(ReceiverService receiver, Message message)
            : base(receiver, message.Folder)
        {
            Message = message;
        }

        protected override Task<Message> Execute(CancellationToken cancellationToken)
        {
            return Receiver.AppendDraftMessageAsync(Message, cancellationToken);
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            if (FolderPath == null)
            {
                return null;
            }

            return this.GetType().Name + email + FolderPath.FullName + Message.Id.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal class ReplaceDraftMessageCommand : ReceiverCommand<Message>
    {
        private Message Message;
        private uint UID;

        public ReplaceDraftMessageCommand(ReceiverService receiver, Message message, uint uid)
            : base(receiver, message.Folder)
        {
            Message = message;
            UID = uid;
        }

        protected override Task<Message> Execute(CancellationToken cancellationToken)
        {
            return Receiver.ReplaceDraftMessageAsync(UID, Message, cancellationToken);
        }

        protected override string GetUniqueCommandIdentifier(string email)
        {
            return this.GetType().Name + email + FolderPath.FullName + UID.ToString(CultureInfo.InvariantCulture);
        }
    }
}
