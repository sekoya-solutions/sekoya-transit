#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class SimulationSpeedSelectionTests
    {
        [Test]
        public void CurrentMultiplier_DefaultsToX1()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            var selection = new SimulationSpeedSelection(
                () => loop.SpeedMultiplier,
                multiplier => loop.SpeedMultiplier = multiplier);

            Assert.That(selection.CurrentMultiplier, Is.EqualTo(1));
            Assert.That(selection.IsSelected(1), Is.True);
        }

        [Test]
        public void Select_EachSupportedSpeed_UpdatesSimulationLoop()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            var selection = new SimulationSpeedSelection(
                () => loop.SpeedMultiplier,
                multiplier => loop.SpeedMultiplier = multiplier);

            foreach (int multiplier in new[] { 1, 2, 3 })
            {
                selection.Select(multiplier);

                Assert.That(loop.SpeedMultiplier, Is.EqualTo(multiplier));
                Assert.That(selection.IsSelected(multiplier), Is.True);
            }
        }

        [Test]
        public void Select_UnsupportedSpeed_ThrowsWithoutChangingSimulationLoop()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            var selection = new SimulationSpeedSelection(
                () => loop.SpeedMultiplier,
                multiplier => loop.SpeedMultiplier = multiplier);

            Assert.That(() => selection.Select(4), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(loop.SpeedMultiplier, Is.EqualTo(1));
        }
    }
}
