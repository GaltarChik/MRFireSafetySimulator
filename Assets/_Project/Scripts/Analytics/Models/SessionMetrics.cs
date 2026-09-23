using System;

namespace MRFireSafety.Analytics.Models
{
    /// <summary>
    /// Serializable summary of one fire-safety training run. Fields are public to remain compatible
    /// with Unity's built-in JSON serializer and are written only when the session ends.
    /// </summary>
    [Serializable]
    public sealed class SessionMetrics
    {
        /// <summary>
        /// Identifies the training scenario used for this session.
        /// </summary>
        public string ScenarioId;

        /// <summary>
        /// Stores the UTC timestamp at which the session started, in ISO 8601 format.
        /// </summary>
        public string StartedAtUtc;

        /// <summary>
        /// Stores the duration of the session in seconds.
        /// </summary>
        public float DurationSeconds;

        /// <summary>
        /// Stores the simulated extinguishing agent consumed during the session.
        /// </summary>
        public float AgentConsumed;

        /// <summary>
        /// Stores the normalized integrity of the protected virtual object.
        /// </summary>
        public float ObjectIntegrity;

        /// <summary>
        /// Stores the final normalized average fire intensity.
        /// </summary>
        public float FinalFireIntensity;

        /// <summary>
        /// Indicates whether the fire was reduced below the completion threshold.
        /// </summary>
        public bool IsFireSuppressed;

        /// <summary>
        /// Stores the average frames per second measured during the session.
        /// </summary>
        public float AverageFramesPerSecond;

        /// <summary>
        /// Stores the lowest sampled frames per second measured during the session.
        /// </summary>
        public float MinimumFramesPerSecond;

        /// <summary>
        /// Stores the frame rate the device was expected to sustain, normally the refresh rate
        /// reported by the XR display. Frame-rate samples are only interpretable against it.
        /// </summary>
        public float TargetFramesPerSecond;

        /// <summary>
        /// Indicates how the session ended: suppressed, agent depleted, object destroyed, or
        /// interrupted when the application lost focus.
        /// </summary>
        public string Outcome;

        /// <summary>
        /// Stores the hardware profile associated with this session.
        /// </summary>
        public DeviceProfile DeviceProfile;
    }
}
