using System;

namespace MRFireSafety.Analytics.Models
{
    /// <summary>
    /// Serializable snapshot of device capabilities used to contextualize session performance data.
    /// </summary>
    [Serializable]
    public sealed class DeviceProfile
    {
        /// <summary>
        /// Stores the reported device name.
        /// </summary>
        public string DeviceName;

        /// <summary>
        /// Stores the operating-system description.
        /// </summary>
        public string OperatingSystem;

        /// <summary>
        /// Stores the graphics-device name.
        /// </summary>
        public string GraphicsDeviceName;

        /// <summary>
        /// Stores the installed system memory in megabytes.
        /// </summary>
        public int SystemMemoryMegabytes;

        /// <summary>
        /// Stores the processor count reported by the target device.
        /// </summary>
        public int ProcessorCount;
    }
}
