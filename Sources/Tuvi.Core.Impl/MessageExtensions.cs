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

using Tuvi.Core.Entities;

namespace Tuvi.Core.Impl
{
    internal static class MessageExtensions
    {
        public static void CopyInitialParameters(this Message message, Message destination)
        {
            destination.Pk = message.Pk;
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
