#nullable enable
using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Map
{
    /// <summary>
    /// Guards the per-frame refresh paths of the map against managed allocations (Issue #70). Each path runs once
    /// first, so one-time setup (label creation, buffer growth, JIT) is not counted.
    /// </summary>
    public sealed class PerFrameAllocationTests
    {
        [Test]
        public void GameDateTextCache_SameDate_ReusesTheTextWithoutAllocating()
        {
            var cache = new GameDateTextCache();
            var date = new GameDate(1900, 3, 14);
            string first = cache.Get(date);

            Assert.That(new TestDelegate(() => cache.Get(date)), DoesNotAllocate());
            Assert.That(cache.Get(date), Is.SameAs(first));
            Assert.That(first, Is.EqualTo(date.ToString()));
        }

        [Test]
        public void GameDateTextCache_NewDate_ReturnsTheNewDateText()
        {
            var cache = new GameDateTextCache();
            cache.Get(new GameDate(1900, 3, 14));

            Assert.That(cache.Get(new GameDate(1900, 3, 15)), Is.EqualTo(new GameDate(1900, 3, 15).ToString()));
        }

        [Test]
        public void IndustryCargoLabelSync_Unchanged_AllocatesNothingAndShowsEachTotal()
        {
            GeneratedWorld world = CreateWorld();
            var parent = new GameObject("IndustryLabelsTestParent");
            try
            {
                using var view = new MapIndustryCargoLabelView(world, parent.transform);
                view.Sync();

                Assert.That(() => view.Sync(), DoesNotAllocate());
                TextMesh[] labels = parent.GetComponentsInChildren<TextMesh>();
                Assert.That(labels.Length, Is.EqualTo(world.Industries.Count));
                for (int index = 0; index < labels.Length; index++)
                {
                    Assert.That(labels[index].text, Is.EqualTo("0"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void VehicleViewSync_NoVehicles_AllocatesNothing()
        {
            GeneratedWorld world = CreateWorld();
            using var view = new MapVehicleView(world, null);
            view.Sync();

            Assert.That(() => view.Sync(), DoesNotAllocate());
        }

        [Test]
        public void RailWagonTrailSampleWagons_RepeatedCall_AllocatesNothing()
        {
            var trail = new RailWagonTrailHistory(maxPoints: 64, minRecordDistance: 0.01f);
            for (int step = 0; step < 20; step++)
            {
                trail.Record(step * 0.1f, 0f);
            }

            var outX = new float[3];
            var outY = new float[3];
            trail.SampleWagons(2f, 0f, 3, 0.5f, outX, outY);

            Assert.That(
                () => trail.SampleWagons(2f, 0f, 3, 0.5f, outX, outY),
                DoesNotAllocate());
            Assert.That(outX[0], Is.EqualTo(1.5f).Within(1e-4f));
        }

        private static IResolveConstraint DoesNotAllocate()
        {
            return UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not);
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 4, 24),
                4,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 2, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 60),
                });
            return WorldGenerator.Generate(settings, 70002UL);
        }
    }
}
