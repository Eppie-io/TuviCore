using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail.Impl.Protocols;

// TODO: move tests to the correct namespace
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tuvi.Core.Mail.Tests")]

namespace Tuvi.Core.Mail.Impl
{
    public static class MailBoxCreator
    {
        public static IMailBox Create(Account account, ICredentialsProvider credentialsProvider)
        {
            return new MailBox(account, credentialsProvider);
        }
    }
    internal class MailBox : IMailBox
    {
        protected readonly Account AccountSettings;
        private readonly ICredentialsProvider CredentialsProvider;

        private SenderService Sender;
        private ReceiverService Receiver;

        public bool HasFolderCounters => true;

        public MailBox(Account accountData, ICredentialsProvider credentialsProvider)
        {
            AccountSettings = accountData;
            CredentialsProvider = credentialsProvider;

            Sender = CreateSenderService();
            Receiver = CreateReceiverService();
        }

        private SenderService CreateSenderService()
        {
            return new Protocols.SMTP.SMTPMailService(AccountSettings.OutgoingServerAddress, AccountSettings.OutgoingServerPort);
        }

        private ReceiverService CreateReceiverService()
        {
            return new Protocols.IMAP.IMAPMailService(AccountSettings.IncomingServerAddress, AccountSettings.IncomingServerPort);
        }

        public virtual async Task SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            SendCommand sendCommand = new SendCommand(Sender, message);
            var messageID = await sendCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken).ConfigureAwait(false);

            AppendSentMessageCommand appendSentMessageCommand = new AppendSentMessageCommand(Receiver, null, message, messageID);
            await appendSentMessageCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken).ConfigureAwait(false);
        }

        public Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken)
        {
            GetFoldersStructureCommand getFoldersStructureCommand = new GetFoldersStructureCommand(Receiver);
            return getFoldersStructureCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public Task<Folder> GetDefaultInboxFolderAsync(CancellationToken cancellationToken)
        {
            GetDefaultInboxFolder getDefaultInboxFolder = new GetDefaultInboxFolder(Receiver);
            return getDefaultInboxFolder.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public virtual Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            GetMessagesCommand getMessagesCommand = new GetMessagesCommand(Receiver, folder, count);
            return getMessagesCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public Task<Message> GetMessageByIDAsync(Folder folder, uint id, CancellationToken cancellationToken = default)
        {
            GetMessageByIDCommand getMessageByIDCommand = new GetMessageByIDCommand(Receiver, folder, id);
            return getMessageByIDCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public virtual Task<IReadOnlyList<Message>> GetLaterMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken = default)
        {
            GetLaterMessagesCommand getLaterMessagesCommand = new GetLaterMessagesCommand(Receiver, folder, count, lastMessage);
            return getLaterMessagesCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken = default)
        {
            return GetEarlierMessagesAsync(folder, count, lastMessage, forSync: false, cancellationToken);
        }

        public Task<IReadOnlyList<Message>> GetEarlierMessagesForSynchronizationAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken = default)
        {
            return GetEarlierMessagesAsync(folder, count, lastMessage, forSync: true, cancellationToken);
        }

        public async Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var groupMessages in messages.GroupBy(message => message.Folder))
            {
                MarkMessagesAsReadCommand markMessagesAsReadCommand
                    = new MarkMessagesAsReadCommand(Receiver, groupMessages.Key, groupMessages.Select(message => message.Id).ToList());
                await markMessagesAsReadCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var groupMessages in messages.GroupBy(message => message.Folder))
            {
                MarkMessagesAsUnReadCommand markMessagesAsUnReadCommand
                    = new MarkMessagesAsUnReadCommand(Receiver, groupMessages.Key, groupMessages.Select(message => message.Id).ToList());
                await markMessagesAsUnReadCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folder, bool permanentDelete, CancellationToken cancellationToken)
        {
            DeleteMessagesCommand deleteMessagesCommand = new DeleteMessagesCommand(Receiver, ids, folder, permanentDelete);
            return deleteMessagesCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public Task<Message> AppendDraftMessageAsync(Message message, CancellationToken cancellationToken)
        {
            AppendDraftMessageCommand appendDraftMessageCommand = new AppendDraftMessageCommand(Receiver, message);
            return appendDraftMessageCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public Task<Message> ReplaceDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken)
        {
            ReplaceDraftMessageCommand replaceDraftMessageCommand = new ReplaceDraftMessageCommand(Receiver, message, id);
            return replaceDraftMessageCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }

        public async Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var groupMessages in messages.GroupBy(message => message.Folder))
            {
                FlagMessagesCommand flagMessagesCommand
                    = new FlagMessagesCommand(Receiver, groupMessages.Key, groupMessages.Select(message => message.Id).ToList());
                await flagMessagesCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var groupMessages in messages.GroupBy(message => message.Folder))
            {
                UnflagMessagesCommand unflagMessagesCommand
                    = new UnflagMessagesCommand(Receiver, groupMessages.Key, groupMessages.Select(message => message.Id).ToList());
                await unflagMessagesCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            Sender.Dispose();
            Receiver.Dispose();
        }

        private Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, bool forSync, CancellationToken cancellationToken)
        {
            GetEarlierMessagesCommand getEarlierMessagesCommand = new GetEarlierMessagesCommand(Receiver, folder, count, lastMessage, forSync);
            return getEarlierMessagesCommand.RunCommand(AccountSettings.Email.Address, CredentialsProvider, cancellationToken);
        }
    }
}
