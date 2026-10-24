#nullable enable
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class VehicleListItemTests
    {
        [Test]
        public void Equals_IncludesMonthlyUpkeep()
        {
            var left = new VehicleListItem(
                VehicleListKind.Road,
                1,
                VehicleListStatusKind.Parked,
                0,
                10,
                0L,
                monthlyUpkeep: 200L);
            var differentUpkeep = new VehicleListItem(
                VehicleListKind.Road,
                1,
                VehicleListStatusKind.Parked,
                0,
                10,
                0L,
                monthlyUpkeep: 201L);

            Assert.That(left.Equals(left), Is.True);
            Assert.That(left.Equals(differentUpkeep), Is.False);
        }
    }
}
