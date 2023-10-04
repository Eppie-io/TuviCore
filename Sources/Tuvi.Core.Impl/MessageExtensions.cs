using Tuvi.Core.Entities;

namespace Tuvi.Core.Impl
{
    internal static class MessageExtensions
    {
        public static void CopyInitialParameters(this Message message, Message destination)
        {
            destination.Id = message.Id;
            destination.IsMarkedAsRead = message.IsMarkedAsRead;
            destination.PreviewText = message.PreviewText;
        }

        public static bool IsNoBodyLoaded(this Message message)
        {
            return message.IsUnprotected()
                && message.TextBody == null
                && message.HtmlBody == null;
        }

        private static bool IsUnprotected(this Message message)
        {
            return message.Protection.Type == MessageProtectionType.None;
        }
    }
}
