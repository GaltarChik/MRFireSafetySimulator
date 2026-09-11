using System.Collections;
using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Systems;
using MRFireSafety.Core;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
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

            // Let the fire establish itself above the active-fire threshold before extinguishing it.
            yield return new WaitForSeconds(2f);
            Assert.That(firePropagationSystem.AverageIntensity, Is.GreaterThan(0.1f), "The fire should reach the active threshold before suppression.");

            firePropagationSystem.ApplySuppression(Vector3.zero, 5f, 1f);
            yield return new WaitForSeconds(0.5f);

            Assert.That(reportedMetrics, Is.Not.Null, "Suppressing the fire should end the session and report metrics.");
            Assert.That(reportedMetrics.IsFireSuppressed, Is.True, "The report should record the fire as suppressed.");
            Assert.That(reportedMetrics.DurationSeconds, Is.GreaterThan(0f), "The report should record a positive session duration.");
            Assert.That(sessionDataManager.IsSessionActive, Is.False, "The session should be closed after completion.");
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
