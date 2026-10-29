#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.UI.Shell;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class NewGameMapSizeMappingTests
    {
        [TestCase(NewGameMapSize.Small, MapSize.Small)]
        [TestCase(NewGameMapSize.Medium, MapSize.Medium)]
        [TestCase(NewGameMapSize.Large, MapSize.Large)]
        public void ToCoreMapSize_MapsEachUiSize(NewGameMapSize size, MapSize expected)
        {
            Assert.That(MapPlaySession.ToCoreMapSize(size), Is.EqualTo(expected));
            Assert.That((int)MapPlaySession.ToCoreMapSize(size), Is.EqualTo(NewGameMapSizes.Tiles(size)));
        }

        [Test]
        public void ToCoreMapSize_UnknownSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MapPlaySession.ToCoreMapSize((NewGameMapSize)7));
        }

        [Test]
        public void HasWorld_BeforeStartIsPressed_IsFalse()
        {
            var gameObject = new GameObject("MapPlaySessionTest");
            try
            {
                var session = gameObject.AddComponent<MapPlaySession>();

                Assert.That(session.HasWorld, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
