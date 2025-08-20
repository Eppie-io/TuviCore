using NUnit.Framework;
using System;

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

        [Test]
        public void MakeHybridAddsPubKeyAndUpdatesName()
        {
            var email = new EmailAddress("user@domain.com", "User");
            var hybrid = email.MakeHybrid("pubkey123");
            Assert.That(hybrid.Address, Is.EqualTo("user+pubkey123@domain.com"));
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
            var email = new EmailAddress("user+pubkey@domain.com");
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
            var email = new EmailAddress("user+pubkey@domain.com");
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
            var email = new EmailAddress("user+pubkey@domain.com");
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
            var email = new EmailAddress("user+pubkey@domain.com", "User");
            var original = email.OriginalAddress;
            Assert.That(original.Address, Is.EqualTo("user@domain.com"));
            Assert.That(original.Name, Is.EqualTo("User"));
        }

        [Test]
        public void DecentralizedAddressHybridReturnsPubKey()
        {
            var email = new EmailAddress("user+pubkey@domain.com");
            Assert.That(email.DecentralizedAddress, Is.EqualTo("pubkey"));
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
            var email = new EmailAddress("user+pubkey@domain.com");
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
            var result = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "address", "Name");
            Assert.That(result.Address, Is.EqualTo("address@eppie"));
            Assert.That(result.Name, Is.EqualTo("Name"));
        }

        [Test]
        public void CreateDecentralizedAddressBitcoinAddsPostfix()
        {
            var result = EmailAddress.CreateDecentralizedAddress(NetworkType.Bitcoin, "address", "Name");
            Assert.That(result.Address, Is.EqualTo("address@bitcoin"));
            Assert.That(result.Name, Is.EqualTo("Name"));
        }

        [Test]
        public void CreateDecentralizedAddressUnsupportedThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => EmailAddress.CreateDecentralizedAddress(NetworkType.Unsupported, "address", "Name"));
        }
    }
}