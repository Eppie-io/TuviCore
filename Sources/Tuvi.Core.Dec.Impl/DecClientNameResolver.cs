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
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec.Impl
{
    public sealed class DecClientNameResolver : IEppieNameResolver
    {
        private readonly IDecStorageClient _client;

        public DecClientNameResolver(IDecStorageClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<string> ResolveAsync(string name, CancellationToken cancellationToken)
        {
            return _client.GetAddressByNameAsync(name, cancellationToken);
        }
    }
}
