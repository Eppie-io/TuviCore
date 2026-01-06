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
