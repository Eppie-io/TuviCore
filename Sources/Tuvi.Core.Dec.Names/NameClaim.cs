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
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Tuvi.Core.Dec.Names
{
    /// <summary>
    /// Helpers for the claim-v1 name registration protocol.
    /// </summary>
    /// <remarks>
    /// claim-v1 relies on a canonical representation of a name and a deterministic text payload.
    /// Canonicalization is applied before creating and verifying a claim so that different user inputs
    /// map to the same identifier.
    /// </remarks>
    public static class NameClaim
    {
        /// <summary>
        /// Converts a user-entered name into the canonical form required by claim-v1.
        /// </summary>
        /// <param name="name">User-provided name value.</param>
        /// <returns>
        /// Canonicalized name used by the protocol, or <see cref="string.Empty"/> when <paramref name="name"/>
        /// is <c>null</c>, empty, or whitespace.
        /// </returns>
        /// <remarks>
        /// Canonicalization rules are protocol-specific and must be stable across platforms:
        /// <list type="bullet">
        /// <item><description>Trim leading/trailing whitespace.</description></item>
        /// <item><description>Convert to lowercase using invariant culture.</description></item>
        /// <item><description>Remove spaces and '+' characters.</description></item>
        /// <item><description>Ensure the temporary testnet suffix <c>".test"</c> is present.</description></item>
        /// </list>
        /// </remarks>
        [SuppressMessage(
            "Globalization",
            "CA1308:Normalize strings to uppercase",
            Justification = "Eppie name claim canonical form is lowercase invariant; this is protocol-related normalization.")]
        public static string CanonicalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            name = name.Trim().ToLowerInvariant();
            name = name.Replace(" ", string.Empty);
            name = name.Replace("+", string.Empty);

            // TODO: Remove hardcoded ".test" suffix after Testnet phase is over
            const string testSuffix = ".test";
            if (!name.EndsWith(testSuffix, StringComparison.Ordinal))
            {
                name += testSuffix;
            }

            return name;
        }

        /// <summary>
        /// Builds the exact textual payload that is signed/verified by the claim-v1 protocol.
        /// </summary>
        /// <param name="name">User-provided name value. Will be canonicalized using <see cref="CanonicalizeName"/>.</param>
        /// <param name="publicKey">Public key value that the name is claimed for (encoding defined by the caller/protocol).</param>
        /// <returns>
        /// Deterministic multi-line payload:
        /// <c>claim-v1\nname=&lt;name&gt;\npublicKey=&lt;key&gt;</c>.
        /// </returns>
        /// <remarks>
        /// The payload uses LF (<c>\n</c>) line endings to avoid platform-specific differences.
        /// </remarks>
        public static string BuildClaimV1Payload(string name, string publicKey)
        {
            var nameCanonical = Sanitize(CanonicalizeName(name));
            var pk = Sanitize(publicKey ?? string.Empty);

            var sb = new StringBuilder();
            sb.Append("claim-v1\n");
            sb.Append("name=").Append(nameCanonical).Append('\n');
            sb.Append("publicKey=").Append(pk);
            return sb.ToString();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("=", string.Empty);
        }
    }
}
