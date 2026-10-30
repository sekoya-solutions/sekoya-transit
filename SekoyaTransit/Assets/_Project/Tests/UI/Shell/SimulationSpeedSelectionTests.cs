#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class SimulationSpeedSelectionTests
    {
        [Test]
        public void CurrentMultiplier_DefaultsToX1()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            SimulationSpeedSelection selection = CreateSelection(loop);

            Assert.That(selection.CurrentMultiplier, Is.EqualTo(1));
            Assert.That(selection.IsSelected(1), Is.True);
            Assert.That(selection.IsPaused, Is.False);
        }

        [Test]
        public void Select_EachSupportedSpeed_UpdatesSimulationLoop()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            SimulationSpeedSelection selection = CreateSelection(loop);

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
            SimulationSpeedSelection selection = CreateSelection(loop);

            Assert.That(() => selection.Select(4), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(loop.SpeedMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void TogglePause_PausesThenResumesAtTheSameSpeed()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            SimulationSpeedSelection selection = CreateSelection(loop);
            selection.Select(3);

            selection.TogglePause();

            Assert.That(loop.IsPaused, Is.True);
            Assert.That(selection.IsPaused, Is.True);
            Assert.That(selection.IsSelected(3), Is.False);

            selection.TogglePause();

            Assert.That(loop.IsPaused, Is.False);
            Assert.That(selection.IsSelected(3), Is.True);
        }

        [Test]
        public void Select_WhilePaused_ResumesAtTheChosenSpeed()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            SimulationSpeedSelection selection = CreateSelection(loop);
            selection.TogglePause();

            selection.Select(2);

            Assert.That(loop.IsPaused, Is.False);
            Assert.That(loop.SpeedMultiplier, Is.EqualTo(2));
            Assert.That(selection.IsSelected(2), Is.True);
        }

        [Test]
        public void PausedLoop_DoesNotAdvanceTicks()
        {
            var loop = new SimulationLoop(TimeSpan.FromSeconds(1), Tick.Zero);
            SimulationSpeedSelection selection = CreateSelection(loop);
            selection.TogglePause();
            int advanced = 0;

            loop.Advance(TimeSpan.FromSeconds(5), tick => advanced++);

            Assert.That(advanced, Is.EqualTo(0));
        }

        private static SimulationSpeedSelection CreateSelection(SimulationLoop loop)
        {
            return new SimulationSpeedSelection(
                () => loop.SpeedMultiplier,
                multiplier => loop.SpeedMultiplier = multiplier,
                () => loop.IsPaused,
                paused => loop.IsPaused = paused);
        }
    }
}
