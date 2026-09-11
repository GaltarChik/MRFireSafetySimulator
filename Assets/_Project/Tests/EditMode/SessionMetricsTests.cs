using MRFireSafety.Analytics.Models;
using NUnit.Framework;
using UnityEngine;

namespace MRFireSafety.Tests.EditMode
{
    /// <summary>
    /// Validates that training-report metrics remain compatible with Unity JSON serialization.
    /// </summary>
    public sealed class SessionMetricsTests
    {
        /// <summary>
        /// Verifies that key session metrics survive a JSON serialization round trip.
        /// </summary>
        [Test]
        public void SessionMetrics_JsonRoundTrip_PreservesCoreMetrics()
        {
            SessionMetrics sourceMetrics = new SessionMetrics
            {
                ScenarioId = "ServerRackElectricalFire",
                DurationSeconds = 43.5f,
                AgentConsumed = 2.75f,
                ObjectIntegrity = 0.8f,
                AverageFramesPerSecond = 72f
            };

            string json = JsonUtility.ToJson(sourceMetrics);
            SessionMetrics restoredMetrics = JsonUtility.FromJson<SessionMetrics>(json);

            Assert.That(restoredMetrics.ScenarioId, Is.EqualTo(sourceMetrics.ScenarioId));
            Assert.That(restoredMetrics.DurationSeconds, Is.EqualTo(sourceMetrics.DurationSeconds));
            Assert.That(restoredMetrics.AgentConsumed, Is.EqualTo(sourceMetrics.AgentConsumed));
            Assert.That(restoredMetrics.ObjectIntegrity, Is.EqualTo(sourceMetrics.ObjectIntegrity));
            Assert.That(restoredMetrics.AverageFramesPerSecond, Is.EqualTo(sourceMetrics.AverageFramesPerSecond));
        }
    }
}
