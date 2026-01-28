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
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Tuvi.Core.Dec.Impl;

namespace Tuvi.Core.Mail.Impl.Tests
{
    [TestFixture]
    public class MailboxIdTests
    {
        private const int ExpectedKeyLength = 53;
        private const string ValidBase32E = "aft5f6u8uf42sfjb9buhzbra3rdbc3rdwggwdrwqtfgvegktxh8cc";
        private const string AnotherValidBase32E = "bft5f6u8uf42sfjb9buhzbra3rdbc3rdwggwdrwqtfgvegktxh8cd";

        [Test]
        public void ConstructorNullPublicKeyThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new MailboxId(null));
            Assert.That(ex.ParamName, Is.EqualTo("publicKey"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void ConstructorEmptyOrWhitespacePublicKeyThrowsArgumentException(string publicKey)
        {
            var ex = Assert.Throws<ArgumentException>(() => new MailboxId(publicKey));
            Assert.That(ex.ParamName, Is.EqualTo("publicKey"));
        }

        [TestCase("abc")]
        public void ConstructorRejectsInvalidLength(string publicKey)
        {
            var ex = Assert.Throws<ArgumentException>(() => new MailboxId(publicKey));
            Assert.That(ex.ParamName, Is.EqualTo("publicKey"));
        }

        [Test]
        public void ConstructorRejectsInvalidLengthBoundaries()
        {
            // Boundary check: Length - 1
            string tooShort = ValidBase32E[..(ExpectedKeyLength - 1)];
            var ex1 = Assert.Throws<ArgumentException>(() => new MailboxId(tooShort));
            Assert.That(ex1.ParamName, Is.EqualTo("publicKey"));

            // Boundary check: Length + 1
            string tooLong = ValidBase32E + "a";
            var ex2 = Assert.Throws<ArgumentException>(() => new MailboxId(tooLong));
            Assert.That(ex2.ParamName, Is.EqualTo("publicKey"));
        }

        [TestCase("!")]
        [TestCase("-")]
        [TestCase("_")]
        [TestCase("+")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase("\n")]
        public void ConstructorRejectsInvalidCharacters(string invalidChar)
        {
            // Keep length at ExpectedKeyLength to ensure rejection is due to invalid char, not length.
            string invalidKey = string.Concat(ValidBase32E.AsSpan(0, ExpectedKeyLength - 1), invalidChar);
            var ex = Assert.Throws<ArgumentException>(() => new MailboxId(invalidKey));
            Assert.That(ex.ParamName, Is.EqualTo("publicKey"));
        }

        [Test]
        public void ConstructorRejectsPublicKeyWithWhitespace()
        {
            // Keep length same as valid key (53) to ensure rejection is due to whitespace, not length.
            string keyWithWhitespace = string.Concat(ValidBase32E.AsSpan(0, 1), " ", ValidBase32E.AsSpan(2));
            var ex = Assert.Throws<ArgumentException>(() => _ = new MailboxId(keyWithWhitespace));
            Assert.That(ex.ParamName, Is.EqualTo("publicKey"));
        }

        [Test]
        public void ConstructorRejectsPublicKeyWithInnerWhitespace()
        {
            // Whitespace inside
            // Keep length same as valid key (53) to ensure rejection is due to invalid char, not length.
            string innerSpace = ValidBase32E.Remove(10, 1).Insert(10, " ");
            var ex = Assert.Throws<ArgumentException>(() => new MailboxId(innerSpace));
            Assert.That(ex.ParamName, Is.EqualTo("publicKey"));
        }

        [Test]
        public void ConstructorValidBase32ECreatesInstance()
        {
            var id = new MailboxId(ValidBase32E);
            Assert.That(id, Is.Not.Null);
        }

        [Test]
        public void ToStringReturnsExpectedHash()
        {
            var id = new MailboxId(ValidBase32E);
            var hash = id.ToString();

            var expectedHash = CalculateExpectedHash(ValidBase32E);
            Assert.That(hash, Is.EqualTo(expectedHash));
        }

        [Test]
        public void ToStringIsCaseInsensitiveForPublicKey()
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            string lowerKey = ValidBase32E.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
            string upperKey = ValidBase32E.ToUpperInvariant();

            var idLower = new MailboxId(lowerKey);
            var idUpper = new MailboxId(upperKey);

            Assert.That(idUpper.ToString(), Is.EqualTo(idLower.ToString()));
        }

        [Test]
        public void EqualsReturnsTrueForSamePublicKey()
        {
            var id1 = new MailboxId(ValidBase32E);
            var id2 = new MailboxId(ValidBase32E);

            Assert.That(id1, Is.EqualTo(id2));
            Assert.That(id1.GetHashCode(), Is.EqualTo(id2.GetHashCode()));
            Assert.That(id1 == id2, Is.True);
            Assert.That(id1 != id2, Is.False);
        }

        [Test]
        public void EqualsReturnsTrueForCaseInsensitivePublicKey()
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            var id1 = new MailboxId(ValidBase32E.ToLowerInvariant());
#pragma warning restore CA1308 // Normalize strings to uppercase
            var id2 = new MailboxId(ValidBase32E.ToUpperInvariant());

            Assert.That(id1, Is.EqualTo(id2));
            Assert.That(id1.GetHashCode(), Is.EqualTo(id2.GetHashCode()));
            Assert.That(id1 == id2, Is.True);
        }

        [Test]
        public void EqualsReturnsFalseForDifferentPublicKey()
        {
            var id1 = new MailboxId(ValidBase32E);
            var id2 = new MailboxId(AnotherValidBase32E);

            Assert.That(id1, Is.Not.EqualTo(id2));
            Assert.That(id1 == id2, Is.False);
            Assert.That(id1 != id2, Is.True);
        }

        [Test]
        public void EqualsReturnsFalseForNull()
        {
            var id = new MailboxId(ValidBase32E);
            object other = null;
            Assert.That(EqualsObject(id, other), Is.False);
        }

        private static bool EqualsObject(MailboxId id, object other)
        {
            return id.Equals(other);
        }

        [Test]
        public void EqualsReturnsTrueForSameInstance()
        {
            var id = new MailboxId(ValidBase32E);
            Assert.That(id.Equals(id), Is.True);

            var same = id;
            Assert.That(id == same, Is.True);
            Assert.That(id != same, Is.False);
        }

        [Test]
        public void OperatorsHandleNulls()
        {
            MailboxId left = null;
            MailboxId right = null;

            Assert.That(EqualsWithOperator(left, right), Is.True);
            Assert.That(NotEqualsWithOperator(left, right), Is.False);

            var id = new MailboxId(ValidBase32E);
            Assert.That(EqualsWithOperator(id, null), Is.False);
            Assert.That(EqualsWithOperator(null, id), Is.False);
            Assert.That(NotEqualsWithOperator(id, null), Is.True);
            Assert.That(NotEqualsWithOperator(null, id), Is.True);
        }

        private static bool EqualsWithOperator(MailboxId left, MailboxId right)
        {
            return left == right;
        }

        private static bool NotEqualsWithOperator(MailboxId left, MailboxId right)
        {
            return left != right;
        }

        [Test]
        public void EqualsReturnsFalseForDifferentType()
        {
            var id = new MailboxId(ValidBase32E);
            Assert.That(id.Equals(new object()), Is.False);
        }

        [Test]
        public void GetHashCodeIsConsistent()
        {
            var id = new MailboxId(ValidBase32E);

            var hash1 = id.GetHashCode();
            var hash2 = id.GetHashCode();
            var hash3 = id.GetHashCode();

            Assert.That(hash2, Is.EqualTo(hash1));
            Assert.That(hash3, Is.EqualTo(hash1));
        }

        [Test]
        public void ToStringIsIdempotent()
        {
            var id = new MailboxId(ValidBase32E);

            var str1 = id.ToString();
            var str2 = id.ToString();
            var str3 = id.ToString();

            Assert.That(str2, Is.EqualTo(str1));
            Assert.That(str3, Is.EqualTo(str1));
        }

        [Test]
        public void ToStringReturnsValidHexString()
        {
            var id = new MailboxId(ValidBase32E);
            var hash = id.ToString();

            // SHA256 produces 64 hex characters
            Assert.That(hash, Has.Length.EqualTo(64));
            Assert.That(hash, Does.Match("^[0-9A-F]+$"));
        }

        private static string CalculateExpectedHash(string publicKey)
        {
            // Duplicating logic intentionally to detect unintended algorithm changes.
            // If this test fails after code changes, verify the algorithm change is intentional.
            const string routePrefix = "tuvi.dec.route.v1|";
            var key = publicKey.ToUpperInvariant();
            var input = routePrefix + key;

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", "", StringComparison.OrdinalIgnoreCase);
        }
    }
}
