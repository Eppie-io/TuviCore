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

using Microsoft.Extensions.Logging;

namespace Tuvi.Core.Logging
{
    public static class LoggingExtension
    {
        private static ILoggerFactory _loggerFactory;

        public static ILoggerFactory LoggerFactory
        {
            get
            {
                if (_loggerFactory is null)
                {
                    _loggerFactory = new LoggerFactory();
                }
                return _loggerFactory;
            }
            set { _loggerFactory = value; }
        }
        static class LoggerContainer<T>
        {
            internal static readonly ILogger Logger = LoggerFactory.CreateLogger<T>();
        }

        public static ILogger Log<T>() => LoggerContainer<T>.Logger;
        public static ILogger Log<T>(this T t) => LoggerContainer<T>.Logger;
    }
}
