using System;

namespace MRFireSafety.Fire.Systems
{
    /// <summary>
    /// Immutable result of one fixed cellular-automata step. The type is a readonly struct so the
    /// simulation can publish step data on every tick without allocating managed memory, which
    /// keeps the mobile XR frame budget free of avoidable garbage collection.
    /// </summary>
    public readonly struct FireSimulationStep : IEquatable<FireSimulationStep>
    {
        /// <summary>
        /// Initializes a new step result.
        /// </summary>
        /// <param name="averageIntensity">Average grid intensity after the step, from zero to one.</param>
        /// <param name="deltaTimeSeconds">Simulated time represented by the step, in seconds.</param>
        public FireSimulationStep(float averageIntensity, float deltaTimeSeconds)
        {
            AverageIntensity = averageIntensity;
            DeltaTimeSeconds = deltaTimeSeconds;
        }

        /// <summary>
        /// Gets the average grid intensity after the step in the inclusive range from zero to one.
        /// </summary>
        public float AverageIntensity { get; }

        /// <summary>
        /// Gets the simulated time represented by the step, in seconds. Consumers must scale
        /// per-step effects such as object damage by this value instead of counting events.
        /// </summary>
        public float DeltaTimeSeconds { get; }

        /// <summary>
        /// Determines whether this step equals another step.
        /// </summary>
        /// <param name="other">Step to compare against.</param>
        /// <returns>True when both values match; otherwise false.</returns>
        public bool Equals(FireSimulationStep other)
        {
            return AverageIntensity.Equals(other.AverageIntensity) && DeltaTimeSeconds.Equals(other.DeltaTimeSeconds);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is FireSimulationStep other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (AverageIntensity.GetHashCode() * 397) ^ DeltaTimeSeconds.GetHashCode();
        }

        /// <summary>
        /// Determines whether two steps are equal.
        /// </summary>
        /// <param name="left">First step.</param>
        /// <param name="right">Second step.</param>
        /// <returns>True when both values match; otherwise false.</returns>
        public static bool operator ==(FireSimulationStep left, FireSimulationStep right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two steps differ.
        /// </summary>
        /// <param name="left">First step.</param>
        /// <param name="right">Second step.</param>
        /// <returns>True when the values differ; otherwise false.</returns>
        public static bool operator !=(FireSimulationStep left, FireSimulationStep right)
        {
            return !left.Equals(right);
        }
    }
}
