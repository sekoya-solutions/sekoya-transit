#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Mutable multi-type cargo bag with deterministic stack ordering by cargo id.
    /// </summary>
    public sealed class CargoInventory
    {
        private readonly List<CargoAmount> _stacks;
        private readonly ReadOnlyCollection<CargoAmount> _stacksView;
        private int _totalAmount;

        /// <summary>
        /// Initializes an empty inventory.
        /// </summary>
        public CargoInventory()
        {
            _stacks = new List<CargoAmount>();
            _stacksView = _stacks.AsReadOnly();
        }

        /// <summary>
        /// Initializes an inventory from existing stacks (merged by cargo id).
        /// </summary>
        public CargoInventory(IReadOnlyList<CargoAmount> stacks)
        {
#pragma warning disable CA1510
            if (stacks == null)
            {
                throw new ArgumentNullException(nameof(stacks));
            }
#pragma warning restore CA1510

            _stacks = new List<CargoAmount>();
            _stacksView = _stacks.AsReadOnly();
            for (int index = 0; index < stacks.Count; index++)
            {
                CargoAmount stack = stacks[index];
                Add(stack.CargoId, stack.Amount);
            }
        }

        /// <summary>
        /// Gets cargo stacks sorted by cargo id.
        /// </summary>
        public IReadOnlyList<CargoAmount> Stacks
        {
            get
            {
                return _stacksView;
            }
        }

        /// <summary>
        /// Gets the sum of all stack amounts.
        /// </summary>
        public int TotalAmount
        {
            get
            {
                return _totalAmount;
            }
        }

        /// <summary>
        /// Returns whether the inventory holds no cargo.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return _totalAmount == 0;
            }
        }

        /// <summary>
        /// Adds cargo, merging into an existing stack when the id matches.
        /// </summary>
        public void Add(string cargoId, int amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cargo amount must be positive.");
            }

            int insertAt = _stacks.Count;
            for (int index = 0; index < _stacks.Count; index++)
            {
                int comparison = string.CompareOrdinal(_stacks[index].CargoId, cargoId);
                if (comparison == 0)
                {
                    CargoAmount existing = _stacks[index];
                    _stacks[index] = new CargoAmount(existing.CargoId, checked(existing.Amount + amount));
                    _totalAmount = checked(_totalAmount + amount);
                    return;
                }

                if (comparison > 0)
                {
                    insertAt = index;
                    break;
                }
            }

            _stacks.Insert(insertAt, new CargoAmount(cargoId, amount));
            _totalAmount = checked(_totalAmount + amount);
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> of <paramref name="cargoId"/>.
        /// </summary>
        /// <returns>The amount actually removed.</returns>
        public int TryRemove(string cargoId, int amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cargo amount must be positive.");
            }

            for (int index = 0; index < _stacks.Count; index++)
            {
                if (!string.Equals(_stacks[index].CargoId, cargoId, StringComparison.Ordinal))
                {
                    continue;
                }

                CargoAmount existing = _stacks[index];
                int removed = amount < existing.Amount ? amount : existing.Amount;
                int remaining = existing.Amount - removed;
                if (remaining == 0)
                {
                    _stacks.RemoveAt(index);
                }
                else
                {
                    _stacks[index] = new CargoAmount(existing.CargoId, remaining);
                }

                _totalAmount -= removed;
                return removed;
            }

            return 0;
        }

        /// <summary>
        /// Transfers up to <paramref name="maxUnits"/> from this inventory into
        /// <paramref name="destination"/>, respecting optional destination capacity.
        /// Stacks are drained in ascending cargo-id order.
        /// </summary>
        /// <returns>Total units transferred.</returns>
        public int TransferTo(CargoInventory destination, int maxUnits, int destinationFreeCapacity)
        {
#pragma warning disable CA1510
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
#pragma warning restore CA1510

            if (maxUnits < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxUnits), maxUnits, "Max units cannot be negative.");
            }

            if (destinationFreeCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(destinationFreeCapacity),
                    destinationFreeCapacity,
                    "Free capacity cannot be negative.");
            }

            int remainingBudget = maxUnits;
            if (remainingBudget > destinationFreeCapacity)
            {
                remainingBudget = destinationFreeCapacity;
            }

            int transferred = 0;
            while (remainingBudget > 0 && _stacks.Count > 0)
            {
                CargoAmount stack = _stacks[0];
                int move = stack.Amount < remainingBudget ? stack.Amount : remainingBudget;
                int removed = TryRemove(stack.CargoId, move);
                if (removed == 0)
                {
                    break;
                }

                destination.Add(stack.CargoId, removed);
                transferred += removed;
                remainingBudget -= removed;
            }

            return transferred;
        }

        /// <summary>
        /// Removes all cargo.
        /// </summary>
        public void Clear()
        {
            _stacks.Clear();
            _totalAmount = 0;
        }

        /// <summary>
        /// Creates a deep copy of this inventory.
        /// </summary>
        public CargoInventory Clone()
        {
            return new CargoInventory(_stacks);
        }
    }
}
