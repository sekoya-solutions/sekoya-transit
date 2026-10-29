#nullable enable
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;

namespace SekoyaTransit.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class DeterministicStateHashTests
    {
        private const string _sha256AbcKnownAnswer = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";
        private const string _expectedReplayHash = "ED6500541379335C70D74B348EAEBF9A3C14134C0BB8D294E4F3D7B600AF1669";
        private const ulong _seed = 42;
        private const ulong _sequence = 54;

        [Test]
        public void ComputeHash_ProducesKnownSha256Answer()
        {
            byte[] input = Encoding.ASCII.GetBytes("abc");

            string hash = DeterministicStateHasher.ToHexString(DeterministicStateHasher.ComputeSha256(input));

            Assert.That(hash, Is.EqualTo(_sha256AbcKnownAnswer));
        }

        [Test]
        public void Replay_ProducesSameKnownStateHashForEqualInputs()
        {
            SimulationCommandProcessor<ReplayState> processor = RunReplay(7, 13);
            ReplayState replayedState = processor.Log.Replay(new ReplayState(_seed, _sequence));
            string originalHash = HashState(processor.State);
            string replayedHash = HashState(replayedState);

            Assert.That(originalHash, Is.EqualTo(_expectedReplayHash));
            Assert.That(replayedHash, Is.EqualTo(originalHash));
        }

        [Test]
        public void Replay_DetectsDifferentSeedAndCommandOrder()
        {
            SimulationCommandProcessor<ReplayState> baseline = RunReplay(7, 13);
            SimulationCommandProcessor<ReplayState> differentSeed = RunReplay(7, 13, seed: 43);
            SimulationCommandProcessor<ReplayState> differentOrder = RunReplay(13, 7);

            string baselineHash = HashState(baseline.State);

            Assert.That(HashState(differentSeed.State), Is.Not.EqualTo(baselineHash));
            Assert.That(HashState(differentOrder.State), Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeHash_IncludesCapturedRandomGeneratorState()
        {
            var first = new ReplayState(_seed, _sequence);
            var second = new ReplayState(_seed, _sequence);
            second.Random.NextUInt32();

            Assert.That(HashState(second), Is.Not.EqualTo(HashState(first)));
        }

        private static SimulationCommandProcessor<ReplayState> RunReplay(
            long firstScore,
            long secondScore,
            ulong seed = _seed)
        {
            var processor = new SimulationCommandProcessor<ReplayState>(new ReplayState(seed, _sequence));
            processor.Process(new Tick(1), new ScoreCommand(new Tick(1), firstScore));
            processor.Process(new Tick(2), new ScoreCommand(new Tick(2), secondScore));
            return processor;
        }

        private static string HashState(ReplayState state)
        {
            return DeterministicStateHasher.ToHexString(
                DeterministicStateHasher.ComputeSha256(state.ToCanonicalSnapshot()));
        }

        private sealed class ReplayState
        {
            public ReplayState(ulong seed, ulong sequence)
            {
                Random = new Pcg32Random(seed, sequence);
            }

            public long Score { get; set; }

            public int CompletedActions { get; set; }

            public Tick CurrentTick { get; set; }

            public Pcg32Random Random { get; }

            public byte[] ToCanonicalSnapshot()
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                    {
                        Pcg32State randomState = Random.CapturedState;
                        writer.Write((byte)1);
                        writer.Write(CurrentTick.Value);
                        writer.Write(Score);
                        writer.Write(CompletedActions);
                        writer.Write(randomState.State);
                        writer.Write(randomState.Increment);
                    }

                    return stream.ToArray();
                }
            }
        }

        private sealed class ScoreCommand : ISimulationCommand<ReplayState>
        {
            private readonly Tick _tick;
            private readonly long _score;

            public ScoreCommand(Tick tick, long score)
            {
                _tick = tick;
                _score = score;
            }

            public CommandValidationResult Validate(ReplayState state)
            {
                return _score > 0
                    ? CommandValidationResult.Valid
                    : CommandValidationResult.Rejected("Score must be positive.");
            }

            public void Apply(ReplayState state)
            {
                int randomBonus = state.Random.NextInt32(0, 10);
                state.Score = checked((state.Score * 31) + _score + randomBonus);
                state.CompletedActions = checked(state.CompletedActions + 1);
                state.CurrentTick = _tick;
            }
        }

    }
}
