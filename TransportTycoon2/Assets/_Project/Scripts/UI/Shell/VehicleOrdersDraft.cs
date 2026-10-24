#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Mutable draft of ordered station stops for one selected vehicle (Issue #136).
    /// Presentation applies via commands; this type never touches Core.
    /// </summary>
    public sealed class VehicleOrdersDraft
    {
        private readonly List<int> _stops = new List<int>();
        private readonly ReadOnlyCollection<int> _stopsView;

        /// <summary>
        /// Initializes an empty draft with no vehicle selected.
        /// </summary>
        public VehicleOrdersDraft()
        {
            _stopsView = _stops.AsReadOnly();
        }

        /// <summary>
        /// Gets the selected vehicle kind, or null when none.
        /// </summary>
        public VehicleListKind? Kind { get; private set; }

        /// <summary>
        /// Gets the selected vehicle id, or null when none.
        /// </summary>
        public int? VehicleId { get; private set; }

        /// <summary>
        /// Gets whether a vehicle is selected for route editing.
        /// </summary>
        public bool HasSelection
        {
            get
            {
                return Kind.HasValue && VehicleId.HasValue;
            }
        }

        /// <summary>
        /// Gets the draft stop list (station ids in visit order).
        /// </summary>
        public IReadOnlyList<int> Stops
        {
            get
            {
                return _stopsView;
            }
        }

        /// <summary>
        /// Selects a vehicle and replaces the draft with its current stops.
        /// </summary>
        public void SelectVehicle(VehicleListKind kind, int vehicleId, IReadOnlyList<int> currentStops)
        {
            if (vehicleId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vehicleId), vehicleId, "Vehicle id cannot be negative.");
            }

#pragma warning disable CA1510
            if (currentStops == null)
            {
                throw new ArgumentNullException(nameof(currentStops));
            }
#pragma warning restore CA1510

            Kind = kind;
            VehicleId = vehicleId;
            _stops.Clear();
            for (int index = 0; index < currentStops.Count; index++)
            {
                _stops.Add(currentStops[index]);
            }
        }

        /// <summary>
        /// Clears vehicle selection and the draft stop list.
        /// </summary>
        public void ClearSelection()
        {
            Kind = null;
            VehicleId = null;
            _stops.Clear();
        }

        /// <summary>
        /// Appends a station id to the draft. Requires a selected vehicle.
        /// Duplicate stops are allowed (looping routes may revisit a station).
        /// </summary>
        /// <returns>True when appended.</returns>
        public bool TryAppend(int stationId)
        {
            if (!VehicleId.HasValue)
            {
                return false;
            }

            if (stationId < 0)
            {
                return false;
            }

            _stops.Add(stationId);
            return true;
        }

        /// <summary>
        /// Removes the stop at <paramref name="index"/>.
        /// </summary>
        public bool TryRemoveAt(int index)
        {
            if (index < 0 || index >= _stops.Count)
            {
                return false;
            }

            _stops.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Moves the stop at <paramref name="index"/> one step earlier in the list.
        /// </summary>
        public bool TryMoveUp(int index)
        {
            if (index <= 0 || index >= _stops.Count)
            {
                return false;
            }

            int stationId = _stops[index];
            _stops[index] = _stops[index - 1];
            _stops[index - 1] = stationId;
            return true;
        }

        /// <summary>
        /// Moves the stop at <paramref name="index"/> one step later in the list.
        /// </summary>
        public bool TryMoveDown(int index)
        {
            if (index < 0 || index >= _stops.Count - 1)
            {
                return false;
            }

            int stationId = _stops[index];
            _stops[index] = _stops[index + 1];
            _stops[index + 1] = stationId;
            return true;
        }

        /// <summary>
        /// Copies draft stops into a new array for command application.
        /// </summary>
        public int[] ToStationIdArray()
        {
            return _stops.ToArray();
        }
    }
}
