namespace MRFireSafety.Analytics.Models
{
    /// <summary>
    /// Identifies how a training run finished. The value is written to the session report as text so
    /// that reports stay readable without the application that produced them.
    /// </summary>
    public enum SessionOutcome
    {
        /// <summary>
        /// The run is still in progress.
        /// </summary>
        InProgress = 0,

        /// <summary>
        /// The trainee reduced the fire below the completion threshold.
        /// </summary>
        Suppressed = 1,

        /// <summary>
        /// The extinguisher ran out of agent while the fire was still burning.
        /// </summary>
        AgentDepleted = 2,

        /// <summary>
        /// The fire destroyed the protected object before it was suppressed.
        /// </summary>
        ObjectDestroyed = 3,

        /// <summary>
        /// The run was cut short, for example when the headset was removed or the application lost
        /// focus. Such runs are recorded but should be excluded from performance comparisons.
        /// </summary>
        Interrupted = 4
    }
}
