using System.Collections;
using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Systems;
using MRFireSafety.Core;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
using MRFireSafety.Suppression.Controllers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MRFireSafety.Tests.PlayMode
{
    /// <summary>
    /// Exercises the full training cycle in play mode: ignition, timed fire growth, object damage
    /// accumulated against simulated time, and session completion once the fire is suppressed.
    /// These tests run without an XR device, so they can gate changes before a device build.
    /// </summary>
    public sealed class TrainingSessionCycleTests
    {
        private GameObject _propObject;
        private GameObject _systemsObject;

        /// <summary>
        /// Destroys the objects created for a test case.
        /// </summary>
        /// <returns>An enumerator required by the play-mode test runner.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_systemsObject != null)
            {
                Object.Destroy(_systemsObject);
            }

            if (_propObject != null)
            {
                Object.Destroy(_propObject);
            }

            yield return null;
        }

        /// <summary>
        /// Verifies that an ignited fire spreads across the grid over time.
        /// </summary>
        /// <returns>An enumerator required by the play-mode test runner.</returns>
        [UnityTest]
        public IEnumerator IgnitedFire_SpreadsOverTime()
        {
            FirePropagationSystem firePropagationSystem = CreateFireRig(out _);
            firePropagationSystem.InitializeFire();
            float initialIntensity = firePropagationSystem.AverageIntensity;

            yield return new WaitForSeconds(1.5f);

            Assert.That(firePropagationSystem.AverageIntensity, Is.GreaterThan(initialIntensity),
                "The fire should spread to neighbouring cells while it burns.");
        }

        /// <summary>
        /// Verifies that object integrity decreases while the fire burns, which confirms that damage
        /// is driven by simulated time rather than by the number of intensity notifications.
        /// </summary>
        /// <returns>An enumerator required by the play-mode test runner.</returns>
        [UnityTest]
        public IEnumerator BurningFire_ReducesObjectIntegrityOverTime()
        {
            FirePropagationSystem firePropagationSystem = CreateFireRig(out FireObjectIntegrityController integrityController);
            firePropagationSystem.InitializeFire();

            yield return new WaitForSeconds(1f);
            float integrityAfterOneSecond = integrityController.CurrentIntegrity;

            yield return new WaitForSeconds(1f);
            float integrityAfterTwoSeconds = integrityController.CurrentIntegrity;

            Assert.That(integrityAfterOneSecond, Is.LessThan(1f), "Sustained fire should damage the protected object.");
            Assert.That(integrityAfterTwoSeconds, Is.LessThan(integrityAfterOneSecond), "Damage should keep accumulating while the fire burns.");
        }

        /// <summary>
        /// Verifies that suppressing an established fire completes the session and produces a report.
        /// </summary>
        /// <returns>An enumerator required by the play-mode test runner.</returns>
        [UnityTest]
        public IEnumerator SuppressedFire_CompletesSessionAndReportsMetrics()
        {
            FirePropagationSystem firePropagationSystem = CreateFireRig(out _);
            SessionDataManager sessionDataManager = _systemsObject.AddComponent<SessionDataManager>();
            TrainingSessionController sessionController = _systemsObject.AddComponent<TrainingSessionController>();

            SessionMetrics reportedMetrics = null;
            sessionDataManager.SessionEnded += metrics => reportedMetrics = metrics;

            sessionController.StartSession();
            Assert.That(sessionDataManager.IsSessionActive, Is.True, "Starting a run should open a metrics session.");

            // Let the fire develop before extinguishing it. Completion arms at ignition, so the
            // check here only confirms the fire is genuinely burning.
            yield return new WaitForSeconds(2f);
            Assert.That(firePropagationSystem.AverageIntensity, Is.GreaterThan(0.012f), "The fire should be burning above the arming threshold before suppression.");

            firePropagationSystem.ApplySuppression(Vector3.zero, 5f, 1f);
            yield return new WaitForSeconds(0.5f);

            Assert.That(reportedMetrics, Is.Not.Null, "Suppressing the fire should end the session and report metrics.");
            Assert.That(reportedMetrics.IsFireSuppressed, Is.True, "The report should record the fire as suppressed.");
            Assert.That(reportedMetrics.DurationSeconds, Is.GreaterThan(0f), "The report should record a positive session duration.");
            Assert.That(sessionDataManager.IsSessionActive, Is.False, "The session should be closed after completion.");
        }

        /// <summary>
        /// Verifies that the nozzle ray finds the fire through the prop collider, reduces its
        /// intensity, and draws the corresponding amount from the extinguisher reservoir.
        /// </summary>
        /// <returns>An enumerator required by the play-mode test runner.</returns>
        [UnityTest]
        public IEnumerator NozzleRaycast_HittingFire_ReducesIntensityAndConsumesAgent()
        {
            FirePropagationSystem firePropagationSystem = CreateFireRig(out _);
            _propObject.transform.position = new Vector3(0f, 0f, 2f);
            BoxCollider propCollider = _propObject.AddComponent<BoxCollider>();
            propCollider.isTrigger = true;

            GameObject nozzleObject = new GameObject("TestNozzle");
            nozzleObject.transform.SetParent(_systemsObject.transform, false);
            nozzleObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.forward));

            SuppressionRaycastController raycastController = _systemsObject.AddComponent<SuppressionRaycastController>();
            raycastController.Configure(default, nozzleObject.transform, null, ~0);
            AgentSuppressionManager agentManager = _systemsObject.AddComponent<AgentSuppressionManager>();

            firePropagationSystem.InitializeFire();
            float intensityBeforeSuppression = firePropagationSystem.AverageIntensity;
            float agentBeforeSuppression = agentManager.RemainingAgentCapacity;
            yield return null;

            bool hasHitFire = raycastController.DischargeAgent(0.5f);

            Assert.That(hasHitFire, Is.True, "The nozzle ray should reach the fire through the prop collider.");
            Assert.That(firePropagationSystem.AverageIntensity, Is.LessThan(intensityBeforeSuppression), "A hit should reduce fire intensity.");
            Assert.That(agentManager.RemainingAgentCapacity, Is.LessThan(agentBeforeSuppression), "Discharging should consume extinguishing agent.");
        }

        /// <summary>
        /// Verifies that discharging away from the fire still empties the extinguisher and ends the
        /// run as a failure. Agent is spent by pulling the trigger, so poor aim carries a cost.
        /// </summary>
        /// <returns>An enumerator required by the play-mode test runner.</returns>
        [UnityTest]
        public IEnumerator SprayingAwayFromFire_DepletesAgentAndEndsSessionAsFailure()
        {
            FirePropagationSystem firePropagationSystem = CreateFireRig(out _);
            SessionDataManager sessionDataManager = _systemsObject.AddComponent<SessionDataManager>();

            GameObject nozzleObject = new GameObject("TestNozzle");
            nozzleObject.transform.SetParent(_systemsObject.transform, false);

            // Aim at empty space: nothing to hit, but the extinguisher still discharges.
            nozzleObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.down));

            SuppressionRaycastController raycastController = _systemsObject.AddComponent<SuppressionRaycastController>();
            raycastController.Configure(default, nozzleObject.transform, null, ~0);
            AgentSuppressionManager agentManager = _systemsObject.AddComponent<AgentSuppressionManager>();
            agentManager.ConfigureCapacity(1f);
            TrainingSessionController sessionController = _systemsObject.AddComponent<TrainingSessionController>();

            SessionMetrics reportedMetrics = null;
            sessionDataManager.SessionEnded += metrics => reportedMetrics = metrics;

            sessionController.StartSession();
            yield return null;

            // One second of discharge at the default rate of 0.5 units/s empties a 1 unit reservoir
            // in two applications, while the fire keeps burning untouched.
            raycastController.DischargeAgent(1f);
            raycastController.DischargeAgent(1f);
            yield return new WaitForSeconds(0.5f);

            Assert.That(agentManager.HasAgentRemaining, Is.False, "Spraying at nothing should still empty the extinguisher.");
            Assert.That(reportedMetrics, Is.Not.Null, "Running out of agent while the fire burns should end the run.");
            Assert.That(reportedMetrics.Outcome, Is.EqualTo(SessionOutcome.AgentDepleted.ToString()), "The failure reason should be recorded as agent depletion.");
            Assert.That(reportedMetrics.IsFireSuppressed, Is.False, "The fire was never hit, so it must not be reported as suppressed.");
            Assert.That(firePropagationSystem.AverageIntensity, Is.GreaterThan(0f), "The untouched fire should still be burning.");
        }

        /// <summary>
        /// Verifies that a completed run can be followed by another one, which is how repeated
        /// sessions are collected during evaluation.
        /// </summary>
        /// <returns>An enumerator required by the play-mode test runner.</returns>
        [UnityTest]
        public IEnumerator CompletedSession_CanBeRestarted()
        {
            FirePropagationSystem firePropagationSystem = CreateFireRig(out FireObjectIntegrityController integrityController);
            SessionDataManager sessionDataManager = _systemsObject.AddComponent<SessionDataManager>();
            TrainingSessionController sessionController = _systemsObject.AddComponent<TrainingSessionController>();

            int completedSessionCount = 0;
            sessionDataManager.SessionEnded += _ => completedSessionCount++;

            sessionController.StartSession();
            yield return new WaitForSeconds(2f);
            firePropagationSystem.ApplySuppression(Vector3.zero, 5f, 1f);
            yield return new WaitForSeconds(0.5f);
            Assert.That(completedSessionCount, Is.EqualTo(1), "The first run should complete.");

            sessionController.StartSession();

            Assert.That(sessionDataManager.IsSessionActive, Is.True, "Restarting should open a new metrics session.");
            Assert.That(firePropagationSystem.AverageIntensity, Is.GreaterThan(0f), "Restarting should re-ignite the fire.");
            Assert.That(integrityController.CurrentIntegrity, Is.EqualTo(1f).Within(0.0001f), "Restarting should restore the protected object.");
        }

        private FirePropagationSystem CreateFireRig(out FireObjectIntegrityController integrityController)
        {
            _propObject = new GameObject("TestTrainingProp");
            GameObject fireObject = new GameObject("TestFireSource");
            fireObject.transform.SetParent(_propObject.transform, false);

            FirePropagationSystem firePropagationSystem = fireObject.AddComponent<FirePropagationSystem>();
            integrityController = _propObject.AddComponent<FireObjectIntegrityController>();
            _systemsObject = new GameObject("TestTrainingSystems");
            return firePropagationSystem;
        }
    }
}
