using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tuvi.Core.Logging;

namespace Tuvi.Core
{
    public static class TaskExtensions
    {
        public static async Task DoWithLogAsync<T>(this IEnumerable<Task> tasks)
        {
            var task = await Task.WhenAny(Task.WhenAll(tasks)).ConfigureAwait(false);
            if (task.Exception is null)
            {
                return;
            }
            foreach (var ex in task.Exception.Flatten().InnerExceptions)
            {
                LogException(LoggingExtension.Log<T>(), ex);
            }
        }

        private static Action<ILogger, Exception> s_exceptionLogAction = LoggerMessage.Define(LogLevel.Debug,
                                                                                              new EventId(1, nameof(LogException)),
                                                                                              "Tasks exception: ");

        private static void LogException(ILogger logger, Exception ex)
        {
            s_exceptionLogAction(logger, ex);
        }
    }
}
