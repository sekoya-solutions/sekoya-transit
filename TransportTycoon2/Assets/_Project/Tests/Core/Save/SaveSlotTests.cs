#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveSlotTests
    {
        [Test]
        public void Manual_ValidIdentifier_CreatesDistinctStorageKey()
        {
            SaveSlot first = SaveSlot.Manual("campaign_1");
            SaveSlot second = SaveSlot.Manual("campaign-1");

            Assert.That(first.StorageKey, Is.EqualTo("manual-campaign_1"));
            Assert.That(first.IsAutosave, Is.False);
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(SaveSlot.Autosave.StorageKey, Is.EqualTo("autosave"));
        }

        [Test]
        public void Manual_IdentifierCase_IsNormalizedForPortableStorage()
        {
            Assert.That(SaveSlot.Manual("Campaign"), Is.EqualTo(SaveSlot.Manual("campaign")));
        }

        [TestCase("")]
        [TestCase("../outside")]
        [TestCase("two words")]
        [TestCase("é")]
        public void Manual_InvalidIdentifier_Throws(string identifier)
        {
            Assert.Throws<ArgumentException>(() => SaveSlot.Manual(identifier));
        }

        [Test]
        public void Manual_IdentifierTooLong_Throws()
        {
            string identifier = new string('a', 49);

            Assert.Throws<ArgumentException>(() => SaveSlot.Manual(identifier));
        }

        [Test]
        public void TryParseStorageKey_ValidKeys_RoundTripSlots()
        {
            SaveSlot[] expected = { SaveSlot.Autosave, SaveSlot.Manual("manual_2") };

            foreach (SaveSlot slot in expected)
            {
                Assert.That(SaveSlot.TryParseStorageKey(slot.StorageKey, out SaveSlot parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(slot));
            }
        }

        [TestCase("../outside")]
        [TestCase("manual-")]
        [TestCase("invalid")]
        public void TryParseStorageKey_InvalidKeys_ReturnsFalse(string storageKey)
        {
            Assert.That(SaveSlot.TryParseStorageKey(storageKey, out _), Is.False);
        }
    }
}
