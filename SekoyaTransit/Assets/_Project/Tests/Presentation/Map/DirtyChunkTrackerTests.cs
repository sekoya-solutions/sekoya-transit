#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class DirtyChunkTrackerTests
    {
        [Test]
        public void MarkTileDirty_SameChunkTwice_CountsOnce()
        {
            var tracker = new DirtyChunkTracker(MapSize.Small);
            tracker.MarkTileDirty(0, 0);
            tracker.MarkTileDirty(31, 31);
            Assert.That(tracker.DirtyCount, Is.EqualTo(1));
        }

        [Test]
        public void MarkTileDirty_TwoChunks_DrainsSortedIndexes()
        {
            var tracker = new DirtyChunkTracker(MapSize.Small);
            tracker.MarkTileDirty(32, 0);
            tracker.MarkTileDirty(0, 0);

            var drained = new List<int>();
            int count = tracker.DrainDirtyChunkIndexes(drained);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(drained, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(tracker.DirtyCount, Is.EqualTo(0));
        }

        [Test]
        public void MarkAllDirty_SmallMap_MarksSixteenChunks()
        {
            var tracker = new DirtyChunkTracker(MapSize.Small);
            tracker.MarkAllDirty();
            Assert.That(tracker.DirtyCount, Is.EqualTo(16));
        }
    }
}
