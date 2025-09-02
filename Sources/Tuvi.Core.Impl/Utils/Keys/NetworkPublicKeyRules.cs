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

using System;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Utils
{
    internal interface INetworkPublicKeyRules
    {
        bool IsSyntacticallyValid(string value);
        bool IsSemanticallyValid(string value);
        bool IsValid(string value);
    }

    internal sealed class EppieNetworkPublicKeyRules : INetworkPublicKeyRules
    {
        private readonly IEcPublicKeyCodec _codec;

        public EppieNetworkPublicKeyRules(IEcPublicKeyCodec codec)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public bool IsSyntacticallyValid(string value)
        {
            return EppiePublicKeySyntax.IsValid(value);
        }

        public bool IsSemanticallyValid(string value)
        {
            try
            {
                _codec.Decode(value);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (FormatException) { return false; }
        }

        public bool IsValid(string value)
        {
            return IsSyntacticallyValid(value) && IsSemanticallyValid(value);
        }
    }

    internal sealed class BitcoinNetworkPublicKeyRules : INetworkPublicKeyRules
    {
        // TODO: syntactic validation for Bitcoin public key/address segment could be added here later.
        public bool IsSyntacticallyValid(string value)
        {
            return !string.IsNullOrEmpty(value);
        }

        public bool IsSemanticallyValid(string value)
        {
            // TODO: No semantic validation implemented yet.
            return !string.IsNullOrEmpty(value);
        }

        public bool IsValid(string value)
        {
            return IsSyntacticallyValid(value) && IsSemanticallyValid(value);
        }
    }

    internal static class NetworkPublicKeyRulesFactory
    {
        public static INetworkPublicKeyRules Create(NetworkType network, IEcPublicKeyCodec codec)
        {
            switch (network)
            {
                case NetworkType.Eppie:
                    return new EppieNetworkPublicKeyRules(codec);
                case NetworkType.Bitcoin:
                    return new BitcoinNetworkPublicKeyRules();
                default:
                    throw new NotSupportedException($"Unsupported network for public key rules: {network}");
            }
        }
    }
}
