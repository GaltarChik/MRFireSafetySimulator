using MRFireSafety.Analytics.Models;
using UnityEngine;

namespace MRFireSafety.Analytics.Services
{
    /// <summary>
    /// Captures immutable device metadata once per application session without querying system
    /// information during frame updates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeviceProfiler : MonoBehaviour
    {
        private DeviceProfile _deviceProfile;

        /// <summary>
        /// Gets the cached profile of the current device.
        /// </summary>
        public DeviceProfile CurrentProfile => _deviceProfile;

        private void Awake()
        {
            _deviceProfile = CaptureProfile();
        }

        /// <summary>
        /// Captures system and graphics metadata for performance-report context.
        /// </summary>
        /// <returns>A serializable profile describing the current device.</returns>
        public DeviceProfile CaptureProfile()
        {
            return new DeviceProfile
            {
                DeviceName = SystemInfo.deviceModel,
                OperatingSystem = SystemInfo.operatingSystem,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                SystemMemoryMegabytes = SystemInfo.systemMemorySize,
                ProcessorCount = SystemInfo.processorCount
            };
        }
    }
}
