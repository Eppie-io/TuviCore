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
using System.Security.Cryptography;
using System.Text;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Dec.Impl
{
    /// <summary>
    /// Represents a mailbox identifier for decentralized messaging.
    /// This value is derived from the recipient public key and is used for transport/storage addressing,
    /// while the public key itself remains the encryption key.
    /// </summary>
    internal class MailboxId : IEquatable<MailboxId>
    {
        private const string RoutePrefix = "tuvi.dec.route.v1|";
        private readonly string _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="MailboxId"/> class.
        /// </summary>
        /// <param name="publicKey">The Base32E-encoded public key.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="publicKey"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="publicKey"/> is empty/whitespace, or not a valid Base32E string.</exception>
        public MailboxId(string publicKey)
        {
            if (publicKey is null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (string.IsNullOrWhiteSpace(publicKey))
            {
                throw new ArgumentException("Public key cannot be empty", nameof(publicKey));
            }

            if (!EppiePublicKeySyntax.IsValid(publicKey))
            {
                throw new ArgumentException("Public key must be a valid Base32E string", nameof(publicKey));
            }

            _value = CalculateHash(publicKey);
        }

        /// <summary>
        /// Returns the string representation of the mailbox id.
        /// </summary>
        public override string ToString()
        {
            return _value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MailboxId);
        }

        public bool Equals(MailboxId other)
        {
            return other != null && String.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static bool operator ==(MailboxId left, MailboxId right)
        {
            return EqualityComparer<MailboxId>.Default.Equals(left, right);
        }

        public static bool operator !=(MailboxId left, MailboxId right)
        {
            return !(left == right);
        }

        private static string CalculateHash(string publicKey)
        {
            var key = publicKey.ToUpperInvariant();
            var input = RoutePrefix + key;

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(input));
                return StringHelper.BytesToHex(hash);
            }
        }
    }
}
