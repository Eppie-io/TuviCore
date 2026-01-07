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

namespace Tuvi.Core.Entities
{
    /// <summary>
    /// Specifies the sort order for contacts.
    /// </summary>
    public enum ContactsSortOrder
    {
        /// <summary>
        /// Sort by last message date descending (most recent first).
        /// </summary>
        ByTime = 0,

        /// <summary>
        /// Sort by contact name ascending (alphabetically).
        /// </summary>
        ByName = 1,

        /// <summary>
        /// Sort by unread count descending, then by last message date.
        /// </summary>
        ByUnread = 2
    }
}
