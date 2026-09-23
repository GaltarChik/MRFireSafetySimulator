using System.Text;
using MRFireSafety.Fire.Systems;
using NUnit.Framework;
using UnityEngine;

namespace MRFireSafety.Tests.EditMode
{
    /// <summary>
    /// Guards the calibrated session envelope of the cellular-automata fire model. The shipped
    /// tuning constants were selected by the parameter sweep in <see cref="FireModelSweepTests"/>;
    /// these tests assert the properties that make a training run playable, so that retuning cannot
    /// silently produce a fire that is unwinnable, that never ignites, or that ends before the
    /// trainee has acted. Measurements are logged so the calibration table in the thesis can be
    /// regenerated from a test run.
    /// </summary>
    public sealed class FireModelCalibrationTests
    {
        private const float SimulationStepSeconds = 0.12f;

        // Mirrors the shipped thresholds on TrainingSessionController.
        private const float ActiveFireThreshold = 0.012f;
        private const float SuppressedFireThreshold = 0.006f;

        private const float AgentCapacity = 10f;
        private const float AgentRatePerSecond = 0.5f;
        private const float SuppressionRadius = 0.3f;
        private const float MaximumSimulatedSeconds = 240f;

        /// <summary>
        /// Verifies that a freshly ignited fire already sits above the arming threshold and above
        /// the completion threshold. If it did not, a trainee who extinguished the fire early would
        /// leave the session running forever, or the run would complete the instant it started.
        /// </summary>
        [Test]
        public void IgnitedFire_SitsBetweenCompletionThresholds()
        {
            GameObject fireObject = new GameObject("FireIgnitionCalibration");
            FirePropagationSystem firePropagationSystem = fireObject.AddComponent<FirePropagationSystem>();
            firePropagationSystem.InitializeFire();

            float ignitionIntensity = firePropagationSystem.AverageIntensity;
            Debug.Log($"Ignition average intensity: {ignitionIntensity:0.0000} (arming at {ActiveFireThreshold:0.000}, completing below {SuppressedFireThreshold:0.000})");

            Assert.That(ignitionIntensity, Is.GreaterThanOrEqualTo(ActiveFireThreshold),
                "Ignition must arm session completion immediately, otherwise an early extinguish leaves the run unfinishable.");
            Assert.That(ignitionIntensity, Is.GreaterThan(SuppressedFireThreshold),
                "Ignition must sit above the completion threshold, otherwise the run completes the moment it starts.");
            Object.DestroyImmediate(fireObject);
        }

        /// <summary>
        /// Verifies that an unattended fire keeps escalating rather than dying out or saturating
        /// immediately, and reports the growth curve.
        /// </summary>
        [Test]
        public void UnattendedFire_EscalatesSteadily()
        {
            GameObject fireObject = new GameObject("FireGrowthCalibration");
            FirePropagationSystem firePropagationSystem = fireObject.AddComponent<FirePropagationSystem>();
            firePropagationSystem.InitializeFire();

            StringBuilder growthCurve = new StringBuilder("Fire growth curve (seconds: average intensity)\n");
            float elapsedSeconds = 0f;
            float intensityAtTenSeconds = 0f;

            while (elapsedSeconds < 60f)
            {
                firePropagationSystem.AdvanceSimulation(SimulationStepSeconds);
                elapsedSeconds += SimulationStepSeconds;

                if (intensityAtTenSeconds <= 0f && elapsedSeconds >= 10f)
                {
                    intensityAtTenSeconds = firePropagationSystem.AverageIntensity;
                }

                if (Mathf.Abs(elapsedSeconds % 10f) < SimulationStepSeconds)
                {
                    growthCurve.AppendLine($"  {elapsedSeconds,5:0.0} s: {firePropagationSystem.AverageIntensity:0.000}");
                }
            }

            Debug.Log(growthCurve.ToString());

            Assert.That(intensityAtTenSeconds, Is.GreaterThan(ActiveFireThreshold),
                "The fire must keep growing after ignition rather than decaying away.");
            Assert.That(firePropagationSystem.AverageIntensity, Is.GreaterThan(intensityAtTenSeconds),
                "The fire must still be escalating a minute in, so that delay carries a cost.");
            Assert.That(firePropagationSystem.AverageIntensity, Is.LessThan(0.98f),
                "The fire must not saturate the whole surface instantly, or the growth phase is meaningless.");
            Object.DestroyImmediate(fireObject);
        }

        /// <summary>
        /// Verifies that a trainee applying correct technique can extinguish an established fire
        /// within the extinguisher capacity, and that the resulting run lands inside the intended
        /// session length.
        /// </summary>
        [Test]
        public void EstablishedFire_IsExtinguishableWithinSessionEnvelope()
        {
            GameObject fireObject = new GameObject("FireSuppressionCalibration");
            FirePropagationSystem firePropagationSystem = fireObject.AddComponent<FirePropagationSystem>();
            firePropagationSystem.InitializeFire();

            // Approach phase: the trainee observes, retrieves the extinguisher and closes in
            // while the fire develops. This is the dominant contribution to session length.
            float approachSeconds = 0f;
            while (approachSeconds < 40f)
            {
                firePropagationSystem.AdvanceSimulation(SimulationStepSeconds);
                approachSeconds += SimulationStepSeconds;
            }

            float intensityAtEngagement = firePropagationSystem.AverageIntensity;
            float suppressionSeconds = 0f;
            float agentConsumed = 0f;
            float sweepPhase = 0f;

            while (firePropagationSystem.AverageIntensity > SuppressedFireThreshold
                && suppressionSeconds < MaximumSimulatedSeconds
                && agentConsumed < AgentCapacity)
            {
                sweepPhase += SimulationStepSeconds;
                float horizontalOffset = (Mathf.PingPong(sweepPhase / 1.5f, 2f) - 1f) * 0.35f;
                float verticalOffset = (Mathf.PingPong(sweepPhase / 6f, 2f) - 1f) * 0.45f;
                float agentThisStep = AgentRatePerSecond * SimulationStepSeconds;

                firePropagationSystem.ApplySuppression(new Vector3(horizontalOffset, verticalOffset, 0f), SuppressionRadius, agentThisStep);
                firePropagationSystem.AdvanceSimulation(SimulationStepSeconds);
                agentConsumed += agentThisStep;
                suppressionSeconds += SimulationStepSeconds;
            }

            float totalSeconds = approachSeconds + suppressionSeconds;
            Debug.Log(
                "Calibrated session envelope\n" +
                $"  approach: {approachSeconds:0.0} s (intensity at engagement {intensityAtEngagement:0.000})\n" +
                $"  suppression: {suppressionSeconds:0.0} s\n" +
                $"  total: {totalSeconds:0.0} s\n" +
                $"  agent used: {agentConsumed:0.00} of {AgentCapacity:0.0}\n" +
                $"  final intensity: {firePropagationSystem.AverageIntensity:0.0000}");

            Assert.That(firePropagationSystem.AverageIntensity, Is.LessThanOrEqualTo(SuppressedFireThreshold),
                "Correct technique must be able to extinguish the fire, otherwise the run is unwinnable.");
            Assert.That(agentConsumed, Is.LessThan(AgentCapacity),
                "Extinguishing must fit inside the extinguisher capacity, with margin for imperfect aim.");
            Assert.That(totalSeconds, Is.InRange(40f, 90f),
                "A well-executed run should last between 40 and 90 seconds.");
            Object.DestroyImmediate(fireObject);
        }
    }
}
