using NUnit.Framework;

namespace Tuvi.Core.Entities.Test
{
    public class StringHelperTests
    {
        [Test]
        public void AreEmailsEqualOneArgumentIsNull()
        {
            Assert.That(StringHelper.AreEmailsEqual("some@email.com", null), Is.False);
        }

        [Test]
        public void AreEmailsEqualTwoArgumentsAreNull()
        {
            Assert.That(StringHelper.AreEmailsEqual(null, null), Is.True);
        }
    }
}
