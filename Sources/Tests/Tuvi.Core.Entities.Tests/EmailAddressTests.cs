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
using NUnit.Framework;

namespace Tuvi.Core.Entities.Test
{
    public class EmailAddressTests
    {
        [Test]
        public void ComparisonTest()
        {
            Assert.That(new EmailAddress("Test@address.a"), Is.EqualTo(new EmailAddress("test@address.A")));

            var a = new EmailAddress("A@address.io");
            var b = new EmailAddress("B@address.io");
            Assert.That(a, Is.Not.EqualTo(b));

            var a2 = new EmailAddress("a@address.Io");
            var a3 = new EmailAddress("A@Address.io", "Name");
            var a4 = new EmailAddress("a@Address.io", "Name");
            var a5 = new EmailAddress("a@Address.io", "Name2");
            var b2 = new EmailAddress("b@Address.io", "Name");

            // Test operator overloads
            Assert.That(a != b);
            Assert.That(a2 == a3);
            Assert.That(a3 == a4);
            Assert.That(a4 == a5);
            Assert.That(a3 != b2);
        }

        [Test]
        public void ConstructorNullAddressThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new EmailAddress(null));
        }

        [Test]
        public void ConstructorWithAddressAndNameSetsPropertiesCorrectly()
        {
            var email = new EmailAddress("test@example.com", "Test User");
            Assert.That(email.Address, Is.EqualTo("test@example.com"));
            Assert.That(email.Name, Is.EqualTo("Test User"));
        }

        [Test]
        public void DisplayNameWithoutNameReturnsAddress()
        {
            var email = new EmailAddress("test@example.com");
            Assert.That(email.DisplayName, Is.EqualTo("test@example.com"));
        }

        [Test]
        public void DisplayNameWithNameReturnsFormattedString()
        {
            var email = new EmailAddress("test@example.com", "Test User");
            Assert.That(email.DisplayName, Is.EqualTo("Test User<test@example.com>"));
        }

        [Test]
        public void DisplayNameWithEmptyNameReturnsAddress()
        {
            var email = new EmailAddress("test@example.com", " ");
            Assert.That(email.DisplayName, Is.EqualTo("test@example.com"));
        }

        [Test]
        public void ParseValidEmailWithoutNameReturnsEmailAddress()
        {
            var result = EmailAddress.Parse("test@example.com");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Address, Is.EqualTo("test@example.com"));
            Assert.That(result.Name, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ParseValidEmailWithNameReturnsEmailAddress()
        {
            var result = EmailAddress.Parse("Test User <test@example.com>");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Address, Is.EqualTo("test@example.com"));
            Assert.That(result.Name, Is.EqualTo("Test User"));
        }

        [Test]
        public void ParseInvalidEmailReturnsNull()
        {
            Assert.That(EmailAddress.Parse("invalid_email"), Is.Null);
            Assert.That(EmailAddress.Parse(null), Is.Null); // Though MailAddress would throw, but Parse catches exceptions
        }

        [Test]
        public void EqualsDifferentAddressesReturnsFalse()
        {
            var email1 = new EmailAddress("test1@example.com");
            var email2 = new EmailAddress("test2@example.com");
            Assert.That(email1.Equals(email2), Is.False);
        }

        [Test]
        public void EqualsSameAddressDifferentNamesReturnsTrue()
        {
            var email1 = new EmailAddress("test@example.com", "Name1");
            var email2 = new EmailAddress("test@example.com", "Name2");
            Assert.That(email1.Equals(email2), Is.True);
        }

        [Test]
        public void GetHashCodeSameLowercasedAddressReturnsSameHash()
        {
            var email1 = new EmailAddress("Test@Example.com");
            var email2 = new EmailAddress("test@example.com");
            Assert.That(email1.GetHashCode(), Is.EqualTo(email2.GetHashCode()));
        }

        [Test]
        public void ToStringReturnsDisplayName()
        {
            var email = new EmailAddress("test@example.com", "Test User");
            Assert.That(email.ToString(), Is.EqualTo("Test User<test@example.com>"));
        }


        [Test]
        public void OperatorNotEqualsSameAddressesReturnsFalse()
        {
            var email1 = new EmailAddress("test@example.com");
            var email2 = new EmailAddress("test@example.com");
            Assert.That(email1 != email2, Is.False);
        }

        [Test]
        public void HasSameAddressWithEmailAddressSameCaseInsensitiveReturnsTrue()
        {
            var email1 = new EmailAddress("Test@Example.com");
            var email2 = new EmailAddress("test@example.com");
            Assert.That(email1.HasSameAddress(email2), Is.True);
        }

        [Test]
        public void HasSameAddressWithEmailAddressNullReturnsFalse()
        {
            var email = new EmailAddress("test@example.com");
            Assert.That(email.HasSameAddress((EmailAddress)null), Is.False);
        }

        [Test]
        public void HasSameAddressWithStringSameCaseInsensitiveReturnsTrue()
        {
            var email = new EmailAddress("Test@Example.com");
            Assert.That(email.HasSameAddress("test@example.com"), Is.True);
        }

        [Test]
        public void HasSameAddressWithStringNullReturnsFalse()
        {
            var email = new EmailAddress("test@example.com");
            Assert.That(email.HasSameAddress((string)null), Is.False);
        }

        const string HybridAddressPubKey = "abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvw";
        [Test]
        public void MakeHybridAddsPubKeyAndUpdatesName()
        {
            var email = new EmailAddress("user@domain.com", "User");
            var hybrid = email.MakeHybrid(HybridAddressPubKey);
            Assert.That(hybrid.Address, Is.EqualTo($"user+{HybridAddressPubKey}@domain.com"));
            Assert.That(hybrid.Name, Is.EqualTo("User (Hybrid)"));
        }

        [Test]
        public void CompareToSameAddressReturnsZero()
        {
            var email1 = new EmailAddress("test@example.com");
            var email2 = new EmailAddress("test@example.com");
            Assert.That(email1.CompareTo(email2), Is.EqualTo(0));
        }

        [Test]
        public void CompareToLexicallyGreaterReturnsPositive()
        {
            var email1 = new EmailAddress("b@example.com");
            var email2 = new EmailAddress("a@example.com");
            Assert.That(email1.CompareTo(email2), Is.GreaterThan(0));
        }

        [Test]
        public void CompareToNullReturnsOne()
        {
            var email = new EmailAddress("test@example.com");
            Assert.That(email.CompareTo(null), Is.EqualTo(1));
        }

        [Test]
        public void IsHybridWithPubKeyReturnsTrue()
        {
            var email = new EmailAddress($"user+{HybridAddressPubKey}@domain.com");
            Assert.That(email.IsHybrid, Is.True);
        }

        [Test]
        public void IsHybridWithoutPubKeyReturnsFalse()
        {
            var email = new EmailAddress("user@domain.com");
            Assert.That(email.IsHybrid, Is.False);
        }

        [Test]
        public void IsDecentralizedHybridReturnsTrue()
        {
            var email = new EmailAddress($"user+{HybridAddressPubKey}@domain.com");
            Assert.That(email.IsDecentralized, Is.True);
        }

        [Test]
        public void IsDecentralizedEppieReturnsTrue()
        {
            var email = new EmailAddress("address@eppie");
            Assert.That(email.IsDecentralized, Is.True);
        }

        [Test]
        public void IsDecentralizedBitcoinReturnsTrue()
        {
            var email = new EmailAddress("address@bitcoin");
            Assert.That(email.IsDecentralized, Is.True);
        }

        [Test]
        public void IsDecentralizedRegularReturnsFalse()
        {
            var email = new EmailAddress("user@domain.com");
            Assert.That(email.IsDecentralized, Is.False);
        }

        [Test]
        public void StandardAddressHybridRemovesPubKey()
        {
            var email = new EmailAddress($"user+{HybridAddressPubKey}@domain.com");
            Assert.That(email.StandardAddress, Is.EqualTo("user@domain.com"));
        }

        [Test]
        public void StandardAddressNonHybridUnchanged()
        {
            var email = new EmailAddress("user@domain.com");
            Assert.That(email.StandardAddress, Is.EqualTo("user@domain.com"));
        }

        [Test]
        public void OriginalAddressReturnsNewWithStandardAddress()
        {
            var email = new EmailAddress($"user+{HybridAddressPubKey}@domain.com", "User");
            var original = email.OriginalAddress;
            Assert.That(original.Address, Is.EqualTo("user@domain.com"));
            Assert.That(original.Name, Is.EqualTo("User"));
        }

        [Test]
        public void DecentralizedAddressHybridReturnsPubKey()
        {
            var email = new EmailAddress($"user+{HybridAddressPubKey}@domain.com");
            Assert.That(email.DecentralizedAddress, Is.EqualTo(HybridAddressPubKey));
        }

        [Test]
        public void DecentralizedAddressEppieReturnsAddressWithoutPostfix()
        {
            var email = new EmailAddress("address@eppie");
            Assert.That(email.DecentralizedAddress, Is.EqualTo("address"));
        }

        [Test]
        public void DecentralizedAddressBitcoinReturnsAddressWithoutPostfix()
        {
            var email = new EmailAddress("address@bitcoin");
            Assert.That(email.DecentralizedAddress, Is.EqualTo("address"));
        }

        [Test]
        public void DecentralizedAddressRegularReturnsEmpty()
        {
            var email = new EmailAddress("user@domain.com");
            Assert.That(email.DecentralizedAddress, Is.EqualTo(string.Empty));
        }

        [Test]
        public void NetworkHybridReturnsEppie()
        {
            var email = new EmailAddress($"user+{HybridAddressPubKey}@domain.com");
            Assert.That(email.Network, Is.EqualTo(NetworkType.Eppie));
        }

        [Test]
        public void NetworkEppieReturnsEppie()
        {
            var email = new EmailAddress("address@eppie");
            Assert.That(email.Network, Is.EqualTo(NetworkType.Eppie));
        }

        [Test]
        public void NetworkBitcoinReturnsBitcoin()
        {
            var email = new EmailAddress("address@bitcoin");
            Assert.That(email.Network, Is.EqualTo(NetworkType.Bitcoin));
        }

        [Test]
        public void NetworkRegularReturnsUnsupported()
        {
            var email = new EmailAddress("user@domain.com");
            Assert.That(email.Network, Is.EqualTo(NetworkType.Unsupported));
        }

        [Test]
        public void NetworkCaseInsensitivePostfix()
        {
            var emailEppie = new EmailAddress("address@EPPie");
            var emailBitcoin = new EmailAddress("address@BITCOIN");
            Assert.That(emailEppie.Network, Is.EqualTo(NetworkType.Eppie));
            Assert.That(emailBitcoin.Network, Is.EqualTo(NetworkType.Bitcoin));
        }

        [Test]
        public void CreateDecentralizedAddressEppieAddsPostfix()
        {
            var result = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "address");
            Assert.That(result.Address, Is.EqualTo("address@eppie"));
        }

        [Test]
        public void CreateDecentralizedAddressBitcoinAddsPostfix()
        {
            var result = EmailAddress.CreateDecentralizedAddress(NetworkType.Bitcoin, "address");
            Assert.That(result.Address, Is.EqualTo("address@bitcoin"));
        }

        [Test]
        public void CreateDecentralizedAddressUnsupportedThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => EmailAddress.CreateDecentralizedAddress(NetworkType.Unsupported, "address"));
        }

        [Test]
        public void HasSameAddressWithDifferentCaseReturnsTrue()
        {
            var email1 = new EmailAddress("User.Name@Example.COM");
            var email2 = new EmailAddress("user.name@example.com");
            Assert.That(email1.HasSameAddress(email2), Is.True);
        }

        [Test]
        public void HasSameAddressWithDifferentLocalPartReturnsFalse()
        {
            var email1 = new EmailAddress("user1@example.com");
            var email2 = new EmailAddress("user2@example.com");
            Assert.That(email1.HasSameAddress(email2), Is.False);
        }

        [Test]
        public void NetworkWithMixedCaseHybridAddressReturnsEppie()
        {
            var email = new EmailAddress($"User+{HybridAddressPubKey}@Domain.Com");
            Assert.That(email.Network, Is.EqualTo(NetworkType.Eppie));
        }

        [Test]
        public void CreateDecentralizedAddressWithEmptyAddressThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, ""));
        }

        [Test]
        public void CreateDecentralizedAddressWithWhitespaceAddressThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                EmailAddress.CreateDecentralizedAddress(NetworkType.Bitcoin, "   "));
        }

        [Test]
        public void DecentralizedAddressWithComplexHybridAddressExtractsCorrectPubKey()
        {
            var email = new EmailAddress($"user.name+{HybridAddressPubKey}@sub.domain.com");
            Assert.That(email.DecentralizedAddress, Is.EqualTo(HybridAddressPubKey));
        }

        [Test]
        [TestCase("address@sub.bitcoin.domain")]
        [TestCase("address@sub.eppie.domain")]
        [TestCase("address@bitcoin-domain.com")]
        [TestCase("address@eppie-domain.com")]
        [TestCase("address@domain.bitcoin.com")]
        [TestCase("address@domain.eppie.com")]
        [TestCase("address@bitcoin.eppie.com")]
        [TestCase("address@eppie.bitcoin.com")]
        [TestCase("address@bitcoin.eppie.domain.com")]
        [TestCase("address@eppie.bitcoin.domain.com")]
        public void NetworkWithComplexDomainBitcoinReturnsUnsupported(string address)
        {
            var email = new EmailAddress(address);
            Assert.That(email.Network, Is.EqualTo(NetworkType.Unsupported));
        }

        [Test]
        [TestCase("@eppie")]
        [TestCase("address@eppie")]
        [TestCase("address.sub@eppie")]
        [TestCase("user@eppie")]
        [TestCase("user.name@eppie")]
        [TestCase("user+tag@eppie")]
        [TestCase("user-name@eppie")]
        [TestCase("user123@eppie")]
        [TestCase("user@Eppie")]
        [TestCase("user@ePpIe")]
        [TestCase("user@EPPie")]
        [TestCase("user@eppie")]
        [TestCase("user!#$%&'*+-/=?^_`{|}~@eppie")]
        public void NetworkWithComplexDomainBitcoinReturnsEppie(string address)
        {
            var email = new EmailAddress(address);
            Assert.That(email.Network, Is.EqualTo(NetworkType.Eppie));
        }

        [Test]
        [TestCase("@bitcoin")]
        [TestCase("address@bitcoin")]
        [TestCase("address.sub@bitcoin")]
        [TestCase("user@bitcoin")]
        [TestCase("user.name@bitcoin")]
        [TestCase("user+tag@bitcoin")]
        [TestCase("user-name@bitcoin")]
        [TestCase("user123@bitcoin")]
        [TestCase("user@BitCoin")]
        [TestCase("user@BITcoin")]
        [TestCase("user@BITCOIN")]
        [TestCase("user@bitcoin")]
        [TestCase("user!#$%&'*+-/=?^_`{|}~@bitcoin")]
        public void NetworkWithComplexDomainBitcoinReturnsBitcoin(string address)
        {
            var email = new EmailAddress(address);
            Assert.That(email.Network, Is.EqualTo(NetworkType.Bitcoin));
        }

        [Test]
        [TestCase("user@subdomain.eppie.com")]
        [TestCase("user@subdomain.bitcoin.com")]
        [TestCase("user@eppie.domain.com")]
        [TestCase("user@bitcoin.domain.com")]
        [TestCase("user@domain.eppie.com")]
        [TestCase("user@domain.bitcoin.com")]
        [TestCase("user@eppie-domain.com")]
        [TestCase("user@bitcoin-domain.com")]
        [TestCase("user@eppie.bitcoin.com")]
        [TestCase("user@bitcoin.eppie.com")]
        public void IsDecentralizedWithValidNetworkButSubdomainReturnsFalse(string address)
        {
            var email = new EmailAddress(address);
            Assert.That(email.IsDecentralized, Is.False);
        }

        [Test]
        public void HasSameAddressWithEmptyEmailsReturnsFalse()
        {
            var email = new EmailAddress("user@domain.com");
            Assert.That(email.HasSameAddress(string.Empty), Is.False);
        }


        [Test]
        public void MakeHybridWithExistingPlusInAddressThrows()
        {
            const string NewHybridAddressPubKey = "aaaaaaahijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvw";
            var email = new EmailAddress($"user+{HybridAddressPubKey}@domain.com", "User");

            Assert.Throws<NotSupportedException>(() => email.MakeHybrid(NewHybridAddressPubKey));
        }

        [Test]
        public void MakeHybridWithExistingPlusInAddressHandlesCorrectly()
        {
            const string NewHybridAddressPubKey = "aaaaaaahijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvw";
            var email = new EmailAddress($"user@domain.com", "User");
            var hybrid = email.MakeHybrid(NewHybridAddressPubKey);

            Assert.That(hybrid.Address, Is.EqualTo($"user+{NewHybridAddressPubKey}@domain.com"));
            Assert.That(hybrid.Name, Is.EqualTo("User (Hybrid)"));
        }

        [Test]
        public void DisplayNameWithSpecialCharactersInNameFormatsCorrectly()
        {
            var email = new EmailAddress("test@example.com", "Test < User >");
            Assert.That(email.DisplayName, Is.EqualTo("Test < User ><test@example.com>"));
        }

        [Test]
        public void ParseWithSpecialCharactersInDisplayNameParsesCorrectly()
        {
            var result = EmailAddress.Parse("Test (User) <test@example.com>");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Address, Is.EqualTo("test@example.com"));
            Assert.That(result.Name, Is.EqualTo("Test (User)"));
        }

        [Test]
        public void DecentralizedAddressWithMixedCasePostfixExtractsCorrectly()
        {
            var email = new EmailAddress("address@EPPie");
            Assert.That(email.DecentralizedAddress, Is.EqualTo("address"));
        }

        [Test]
        public void GetHashCodeWithSameAddressDifferentNameReturnsSameHash()
        {
            var email1 = new EmailAddress("test@example.com", "Name1");
            var email2 = new EmailAddress("test@example.com", "Name2");
            Assert.That(email1.GetHashCode(), Is.EqualTo(email2.GetHashCode()));
        }

        [Test]
        public void IsHybridWithMultiplePlusSegmentsReturnsFalse()
        {
            var email = new EmailAddress($"user+{HybridAddressPubKey}+extra@domain.com");

            var isHybrid = email.IsHybrid;

            Assert.That(isHybrid, Is.False);
        }

        [Test]
        [TestCase("abcdefghijkmnpqrstuvwxyzo3456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyzl3456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz13456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz03456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuv")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstu")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrst")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrs")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwwww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwwwww")]
        public void StandardAddressWithInvalidHybridCandidateReturnsOriginal(string invalidLengthPubKey)
        {
            var addr = $"user+{invalidLengthPubKey}@domain.com";
            var email = new EmailAddress(addr);

            var standard = email.StandardAddress;
            var isHybrid = email.IsHybrid;

            Assert.That(isHybrid, Is.False);
            Assert.That(standard, Is.EqualTo(addr));
        }

        [Test]
        [TestCase("abcdefghijkmnpqrstuvwxyzo3456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyzl3456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz13456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz03456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuv")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstu")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrst")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrs")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwwww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwwwww")]
        public void DecentralizedAddressInvalidHybridReturnsEmpty(string invalidCharPubKey)
        {
            var email = new EmailAddress($"user+{invalidCharPubKey}@domain.com");

            var isHybrid = email.IsHybrid;
            var dec = email.DecentralizedAddress;

            Assert.That(isHybrid, Is.False);
            Assert.That(dec, Is.EqualTo(string.Empty));
        }

        [Test]
        public void IsHybridMixedCasePubKeyReturnsTrue()
        {
            var firstPartUpper = new string(HybridAddressPubKey.AsSpan(0, 10)).ToUpperInvariant();
            var mixedCasePubKey = string.Concat(firstPartUpper, HybridAddressPubKey.AsSpan(10));
            var email = new EmailAddress($"user+{mixedCasePubKey}@domain.com");

            var isHybrid = email.IsHybrid;

            Assert.That(isHybrid, Is.True);
        }

        [Test]
        [TestCase("abcdefghijkmnpqrstuvwxyzo3456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyzl3456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz13456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz03456789abcdefghijkmnpqrstuvw")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuv")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstu")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrst")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrs")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwwww")]
        [TestCase("abcdefghijkmnpqrstuvwxyz23456789abcdefghijkmnpqrstuvwwwww")]
        public void MakeHybridFromAddressWithInvalidExistingCandidateWorks(string invalidLengthPubKey)
        {
            var baseEmail = new EmailAddress($"user+{invalidLengthPubKey}@domain.com", "User");
            Assert.That(baseEmail.IsHybrid, Is.False); // sanity

            var newHybrid = baseEmail.MakeHybrid(HybridAddressPubKey);

            Assert.That(newHybrid.IsHybrid, Is.True);
            Assert.That(newHybrid.Address, Is.EqualTo($"user+{HybridAddressPubKey}@domain.com"));
            Assert.That(newHybrid.Name, Is.EqualTo("User (Hybrid)"));
        }
    }
}
