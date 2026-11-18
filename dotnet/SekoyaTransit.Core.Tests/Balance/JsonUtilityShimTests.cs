#nullable enable
using System;
using NUnit.Framework;

namespace SekoyaTransit.Core.Tests.Balance
{
    /// <summary>
    /// The balance tool's stand-in for Unity's <c>JsonUtility.ToJson</c> (Issue #352) follows Unity's serializer rules.
    /// These tests pin those rules; the proof that a whole save matches Unity's writer is the byte-for-byte check in
    /// the balance tool's README.
    /// </summary>
    [TestFixture]
    public sealed class JsonUtilityShimTests
    {
        [Test]
        public void ToJson_FieldsInDeclarationOrder_WritesThemWithoutWhitespace()
        {
            var document = new Document { count = 3, total = 12345678901L, enabled = true, name = "Rail #1" };

            string json = UnityEngine.JsonUtility.ToJson(document);

            Assert.That(
                json,
                Is.EqualTo("{\"count\":3,\"total\":12345678901,\"enabled\":true,\"name\":\"Rail #1\",\"ids\":[],\"names\":[],\"child\":{\"value\":0,\"label\":\"\"},\"children\":[]}"));
        }

        [Test]
        public void ToJson_NullStringArrayAndObject_WritesEmptyValuesLikeUnity()
        {
            var document = new Document { name = null, ids = null, names = null, child = null, children = null };

            string json = UnityEngine.JsonUtility.ToJson(document);

            Assert.That(json, Does.Contain("\"name\":\"\""));
            Assert.That(json, Does.Contain("\"ids\":[]"));
            Assert.That(json, Does.Contain("\"child\":{\"value\":0,\"label\":\"\"}"));
            Assert.That(json, Does.Contain("\"children\":[]"));
        }

        [Test]
        public void ToJson_ArraysOfValuesAndObjects_WritesEachElement()
        {
            var document = new Document
            {
                ids = new[] { 1, -2 },
                names = new[] { "a", null },
                children = new[] { new Child { value = 7, label = "x" }, null },
            };

            string json = UnityEngine.JsonUtility.ToJson(document);

            Assert.That(json, Does.Contain("\"ids\":[1,-2]"));
            Assert.That(json, Does.Contain("\"names\":[\"a\",\"\"]"));
            Assert.That(json, Does.Contain("\"children\":[{\"value\":7,\"label\":\"x\"},{\"value\":0,\"label\":\"\"}]"));
        }

        [Test]
        public void ToJson_SpecialCharacters_EscapesQuotesBackslashesAndControlCharacters()
        {
            var document = new Child { label = "a\"b\\c\nd\te\u0001é/" };

            string json = UnityEngine.JsonUtility.ToJson(document);

            Assert.That(json, Is.EqualTo("{\"value\":0,\"label\":\"a\\\"b\\\\c\\nd\\te\\u0001é/\"}"));
        }

        [Test]
        public void ToJson_SkipsReadonlyAndNonSerializedFields()
        {
            string json = UnityEngine.JsonUtility.ToJson(new WithSkippedFields());

            Assert.That(json, Is.EqualTo("{\"kept\":1}"));
        }

        [Test]
        public void ToJson_UnsupportedFieldType_Throws()
        {
            Assert.That(() => UnityEngine.JsonUtility.ToJson(new WithDouble()), Throws.ArgumentException);
        }

#pragma warning disable CA1051, IDE1006, SA1307, SA1401 // Mirrors the save document's lower-case public fields.
        [Serializable]
        private sealed class Document
        {
            public int count;
            public long total;
            public bool enabled;
            public string? name;
            public int[]? ids;
            public string?[]? names;
            public Child? child;
            public Child?[]? children;
        }

        [Serializable]
        private sealed class Child
        {
            public int value;
            public string? label;
        }

        [Serializable]
        private sealed class WithSkippedFields
        {
            public const int Constant = 2;
            public readonly int readOnly = 3;
            public int kept = 1;
            [NonSerialized]
            public int skipped = 4;
        }

        [Serializable]
        private sealed class WithDouble
        {
            public double ratio = 0.5;
        }
#pragma warning restore CA1051, IDE1006, SA1307, SA1401
    }
}
