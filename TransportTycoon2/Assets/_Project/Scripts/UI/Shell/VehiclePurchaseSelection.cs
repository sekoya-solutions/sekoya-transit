#nullable enable

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Station kind currently selected for the Vehicles Buy button.
    /// </summary>
    public enum VehiclePurchaseSelection
    {
        /// <summary>
        /// No matching road or rail station is selected.
        /// </summary>
        None = 0,

        /// <summary>
        /// A road station is selected (Buy purchases a truck).
        /// </summary>
        Road = 1,

        /// <summary>
        /// A rail station is selected (Buy purchases a locomotive).
        /// </summary>
        Rail = 2
    }
}
