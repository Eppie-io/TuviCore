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
using Tuvi.Core.Dec.Web.Impl;

namespace Tuvi.Core.Dec
{
    public static class DecStorageBuilder
    {
        public static IDecStorageClient CreateWebClient(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL is empty.", nameof(url));
            }

            return new WebDecStorageClient(url);
        }

        public static IDecStorageClient CreateWebClient(Uri url)
        {
            if (url is null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            return CreateWebClient(url.AbsoluteUri);
        }
    }
}
