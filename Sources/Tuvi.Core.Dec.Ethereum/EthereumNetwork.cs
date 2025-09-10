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

namespace Tuvi.Core.Dec.Ethereum
{
    /// <summary>
    /// Known Ethereum networks supported by default.
    /// </summary>
    public enum EthereumNetwork
    {
        /// <summary>
        /// Unspecified network (not used).
        /// </summary>
        None = 0,
        /// <summary>
        /// Ethereum main network (chainId=1).
        /// </summary>
        MainNet = 1,
        /// <summary>
        /// Ethereum Sepolia test network (chainId=11155111).
        /// </summary>
        Sepolia = 11155111
    }
}
