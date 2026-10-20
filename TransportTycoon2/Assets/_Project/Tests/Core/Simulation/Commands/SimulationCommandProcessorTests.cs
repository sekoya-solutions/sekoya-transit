#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;

namespace TransportTycoon.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class SimulationCommandProcessorTests
    {
        private static readonly string[] _expectedCommands = { "first", "second" };

        [Test]
        public void Process_AppliesValidCommandAndRecordsTickAndSequence()
        {
            var state = new TestState();
            var processor = new SimulationCommandProcessor<TestState>(state);

            CommandProcessingResult result = processor.Process(new Tick(3), new AddCommand(5, "first"));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.RejectionReason, Is.Null);
            Assert.That(result.Sequence, Is.EqualTo(1));
            Assert.That(state.Value, Is.EqualTo(5));
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));
            Assert.That(processor.Log.Entries[0].Tick, Is.EqualTo(new Tick(3)));
            Assert.That(processor.Log.Entries[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void Process_RejectsInvalidCommandWithoutMutationOrLogging()
        {
            var state = new TestState();
            var processor = new SimulationCommandProcessor<TestState>(state);

            CommandProcessingResult rejected = processor.Process(new Tick(2), new AddCommand(-1, "rejected"));
            CommandProcessingResult accepted = processor.Process(new Tick(2), new AddCommand(4, "accepted"));

            Assert.That(rejected.IsAccepted, Is.False);
            Assert.That(rejected.RejectionReason, Is.EqualTo("Amount must be positive."));
            Assert.That(rejected.Sequence, Is.Null);
            Assert.That(state.Value, Is.EqualTo(4));
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));
            Assert.That(accepted.Sequence, Is.EqualTo(1));
        }

        [Test]
        public void Process_PreservesSubmissionOrderInLog()
        {
            var state = new TestState();
            var processor = new SimulationCommandProcessor<TestState>(state);

            processor.Process(new Tick(1), new AddCommand(2, "first"));
            processor.Process(new Tick(1), new AddCommand(3, "second"));

            CollectionAssert.AreEqual(_expectedCommands, state.AppliedCommands);
            Assert.That(processor.Log.Entries[0].Sequence, Is.EqualTo(1));
            Assert.That(processor.Log.Entries[1].Sequence, Is.EqualTo(2));
        }

        [Test]
        public void Replay_ReappliesAcceptedCommandsInRecordedOrder()
        {
            var processor = new SimulationCommandProcessor<TestState>(new TestState());
            processor.Process(new Tick(1), new AddCommand(2, "first"));
            processor.Process(new Tick(2), new AddCommand(3, "second"));
            processor.Process(new Tick(2), new AddCommand(-1, "rejected"));

            TestState replayedState = processor.Log.Replay(new TestState());

            Assert.That(replayedState.Value, Is.EqualTo(processor.State.Value));
            CollectionAssert.AreEqual(processor.State.AppliedCommands, replayedState.AppliedCommands);
        }

        [Test]
        public void Process_PropagatesApplyFailureWithoutRecordingCommand()
        {
            var state = new TestState();
            var processor = new SimulationCommandProcessor<TestState>(state);

            Assert.That(
                () => processor.Process(new Tick(1), new ThrowingCommand()),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(state.Value, Is.EqualTo(0));

            CommandProcessingResult accepted = processor.Process(new Tick(1), new AddCommand(2, "accepted"));

            Assert.That(accepted.Sequence, Is.EqualTo(1));
            Assert.That(state.Value, Is.EqualTo(2));
        }

        [Test]
        public void CommandLogEntries_CannotBeModifiedByCaller()
        {
            var processor = new SimulationCommandProcessor<TestState>(new TestState());
            processor.Process(new Tick(1), new AddCommand(1, "first"));

            Assert.That(
                () => ((IList<RecordedCommand<TestState>>)processor.Log.Entries).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void ValidationResult_RejectsEmptyReason()
        {
            Assert.That(
                () => CommandValidationResult.Rejected(" "),
                Throws.TypeOf<ArgumentException>());
        }

        private sealed class TestState
        {
            public int Value { get; set; }

            public List<string> AppliedCommands { get; } = new List<string>();
        }

        private sealed class AddCommand : ISimulationCommand<TestState>
        {
            private readonly int _amount;
            private readonly string _name;

            public AddCommand(int amount, string name)
            {
                _amount = amount;
                _name = name;
            }

            public CommandValidationResult Validate(TestState state)
            {
                return _amount > 0
                    ? CommandValidationResult.Valid
                    : CommandValidationResult.Rejected("Amount must be positive.");
            }

            public void Apply(TestState state)
            {
                state.Value = checked(state.Value + _amount);
                state.AppliedCommands.Add(_name);
            }
        }

        private sealed class ThrowingCommand : ISimulationCommand<TestState>
        {
            public CommandValidationResult Validate(TestState state)
            {
                return CommandValidationResult.Valid;
            }

            public void Apply(TestState state)
            {
                throw new InvalidOperationException("Apply failed.");
            }
        }
    }
}
