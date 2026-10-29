#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// The result of validating a simulation command.
    /// </summary>
    public sealed class CommandValidationResult
    {
        private CommandValidationResult(bool isValid, string? rejectionReason)
        {
            IsValid = isValid;
            RejectionReason = rejectionReason;
        }

        /// <summary>
        /// Gets the result used when a command is valid.
        /// </summary>
        public static CommandValidationResult Valid { get; } = new CommandValidationResult(true, null);

        /// <summary>
        /// Gets whether the command passed validation.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the reason a command was rejected, or <see langword="null"/> when it is valid.
        /// </summary>
        public string? RejectionReason { get; }

        /// <summary>
        /// Creates a rejected validation result.
        /// </summary>
        /// <param name="reason">A non-empty explanation for the rejection.</param>
        /// <returns>The rejected validation result.</returns>
        public static CommandValidationResult Rejected(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A rejection reason must not be empty.", nameof(reason));
            }

            return new CommandValidationResult(false, reason);
        }
    }

    /// <summary>
    /// A deterministic command that validates against and mutates simulation state.
    /// Validation must not mutate state. Implementations should be immutable and must not depend
    /// on presentation or wall-clock state; applying a valid command should not throw.
    /// </summary>
    /// <typeparam name="TState">The reference type representing simulation state.</typeparam>
    public interface ISimulationCommand<TState>
        where TState : class
    {
        /// <summary>
        /// Determines whether the command may be applied to the supplied state.
        /// </summary>
        /// <param name="state">The current simulation state.</param>
        /// <returns>A valid result or an explicit rejection reason.</returns>
        CommandValidationResult Validate(TState state);

        /// <summary>
        /// Applies a command that passed validation.
        /// </summary>
        /// <param name="state">The simulation state to mutate.</param>
        void Apply(TState state);
    }

    /// <summary>
    /// The outcome of processing a simulation command.
    /// </summary>
    public readonly struct CommandProcessingResult
    {
        private CommandProcessingResult(bool isAccepted, string? rejectionReason, long? sequence)
        {
            IsAccepted = isAccepted;
            RejectionReason = rejectionReason;
            Sequence = sequence;
        }

        /// <summary>
        /// Gets whether the command was applied and recorded.
        /// </summary>
        public bool IsAccepted { get; }

        /// <summary>
        /// Gets the validation reason for a rejected command.
        /// </summary>
        public string? RejectionReason { get; }

        /// <summary>
        /// Gets the deterministic log sequence assigned to an accepted command.
        /// </summary>
        public long? Sequence { get; }

        internal static CommandProcessingResult Accepted(long sequence)
        {
            return new CommandProcessingResult(true, null, sequence);
        }

        internal static CommandProcessingResult Rejected(string reason)
        {
            return new CommandProcessingResult(false, reason, null);
        }
    }

    /// <summary>
    /// A command accepted by a processor, with its simulation tick and log sequence.
    /// </summary>
    /// <typeparam name="TState">The reference type representing simulation state.</typeparam>
    public sealed class RecordedCommand<TState>
        where TState : class
    {
        internal RecordedCommand(Tick tick, long sequence, ISimulationCommand<TState> command)
        {
            Tick = tick;
            Sequence = sequence;
            Command = command;
        }

        /// <summary>
        /// Gets the simulation tick at which the command was processed.
        /// </summary>
        public Tick Tick { get; }

        /// <summary>
        /// Gets the one-based sequence number assigned in processing order.
        /// </summary>
        public long Sequence { get; }

        /// <summary>
        /// Gets the accepted command.
        /// </summary>
        public ISimulationCommand<TState> Command { get; }
    }

    /// <summary>
    /// An in-memory, append-only log of accepted simulation commands.
    /// </summary>
    /// <typeparam name="TState">The reference type representing simulation state.</typeparam>
    public sealed class CommandLog<TState>
        where TState : class
    {
        private readonly List<RecordedCommand<TState>> _entries = new List<RecordedCommand<TState>>();
        private readonly ReadOnlyCollection<RecordedCommand<TState>> _readOnlyEntries;

        /// <summary>
        /// Initializes an empty command log.
        /// </summary>
        public CommandLog()
        {
            _readOnlyEntries = _entries.AsReadOnly();
        }

        /// <summary>
        /// Gets the accepted commands in deterministic processing order.
        /// </summary>
        public IReadOnlyList<RecordedCommand<TState>> Entries
        {
            get
            {
                return _readOnlyEntries;
            }
        }

        /// <summary>
        /// Replays the accepted commands against a separate equivalent initial state.
        /// </summary>
        /// <param name="initialState">The equivalent initial state for replay.</param>
        /// <returns>The state after all logged commands have been applied.</returns>
        public TState Replay(TState initialState)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }
#pragma warning restore CA1510

            long expectedSequence = 0;
            foreach (RecordedCommand<TState> entry in _entries)
            {
                long nextSequence = checked(expectedSequence + 1);
                if (entry.Sequence != nextSequence)
                {
                    throw new InvalidOperationException("Command log sequence is not contiguous.");
                }

                CommandValidationResult validation = entry.Command.Validate(initialState);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(
                        "Recorded command at sequence " + entry.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " is no longer valid: " + validation.RejectionReason);
                }

                entry.Command.Apply(initialState);
                expectedSequence = entry.Sequence;
            }

            return initialState;
        }

        internal void Append(RecordedCommand<TState> entry)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>
    /// Validates and applies commands in submission order, recording accepted commands for replay.
    /// </summary>
    /// <typeparam name="TState">The reference type representing simulation state.</typeparam>
    public sealed class SimulationCommandProcessor<TState>
        where TState : class
    {
        private long _lastSequence;

        /// <summary>
        /// Initializes a processor for the supplied simulation state.
        /// </summary>
        /// <param name="initialState">The state that commands will validate and operate on.</param>
        public SimulationCommandProcessor(TState initialState)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }
#pragma warning restore CA1510

            State = initialState;
            Log = new CommandLog<TState>();
        }

        /// <summary>
        /// Gets the simulation state owned by this processor.
        /// </summary>
        public TState State { get; }

        /// <summary>
        /// Gets the append-only log of accepted commands.
        /// </summary>
        public CommandLog<TState> Log { get; }

        /// <summary>
        /// Validates and processes one command. A validation rejection does not apply or record it.
        /// Exceptions from command application are propagated and the command is not recorded;
        /// mutations performed before an exception are not rolled back.
        /// </summary>
        /// <param name="tick">The simulation tick at which the command is processed.</param>
        /// <param name="command">The command to validate and apply.</param>
        /// <returns>The acceptance or rejection result.</returns>
        public CommandProcessingResult Process(Tick tick, ISimulationCommand<TState> command)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
#pragma warning restore CA1510

            CommandValidationResult validation = command.Validate(State);
            if (!validation.IsValid)
            {
                return CommandProcessingResult.Rejected(validation.RejectionReason!);
            }

            long sequence = checked(_lastSequence + 1);
            command.Apply(State);
            Log.Append(new RecordedCommand<TState>(tick, sequence, command));
            _lastSequence = sequence;
            return CommandProcessingResult.Accepted(sequence);
        }
    }
}
