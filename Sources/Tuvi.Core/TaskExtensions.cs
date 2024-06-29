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
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aggregateException)
                {
                    foreach (var innerEx in aggregateException.Flatten().InnerExceptions)
                    {
                        LogException(LoggingExtension.Log<T>(), innerEx);
                    }
                }
                else
                {
                    LogException(LoggingExtension.Log<T>(), ex);
                }

                throw;
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
