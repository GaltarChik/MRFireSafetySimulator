using System;
using System.IO;
using System.Threading.Tasks;
using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Services;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
using MRFireSafety.Suppression.Controllers;
using UnityEngine;

namespace MRFireSafety.Analytics.Systems
{
    /// <summary>
    /// Collects session-level performance metrics and persists one JSON report when a training run
    /// ends. The session lifecycle is driven by the training session controller rather than by scene
    /// load, so the recorded duration measures extinguishing work and not room scanning. File I/O is
    /// asynchronous and never executes from the frame-update loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionDataManager : MonoBehaviour
    {
        [SerializeField] private FirePropagationSystem _firePropagationSystem;
        [SerializeField] private FireObjectIntegrityController _integrityController;
        [SerializeField] private PerformanceProfiler _performanceProfiler;
        [SerializeField] private DeviceProfiler _deviceProfiler;
        [SerializeField] private AgentSuppressionManager _agentSuppressionManager;
        [SerializeField] private SuppressionRaycastController _suppressionController;
        [SerializeField] private string _scenarioId = "ServerRackElectricalFire";
        [SerializeField, Range(0f, 1f)] private float _suppressedIntensityThreshold = 0.05f;

        private SessionMetrics _sessionMetrics;
        private float _sessionStartedAtRealtime;
        private float _lastSessionDurationSeconds;
        private bool _isSessionActive;

        /// <summary>
        /// Raised when a new metrics session begins.
        /// </summary>
        public event Action SessionStarted;

        /// <summary>
        /// Raised after session metrics have been finalized in memory and before the report is written.
        /// </summary>
        public event Action<SessionMetrics> SessionEnded;

        /// <summary>
        /// Gets whether a training session is currently collecting metrics.
        /// </summary>
        public bool IsSessionActive => _isSessionActive;

        /// <summary>
        /// Gets the elapsed time of the active session in seconds, or the duration of the most
        /// recent session once it has ended.
        /// </summary>
        public float ElapsedSessionSeconds => _isSessionActive
            ? Time.realtimeSinceStartup - _sessionStartedAtRealtime
            : _lastSessionDurationSeconds;

        private void Awake()
        {
            if (_firePropagationSystem == null)
            {
                _firePropagationSystem = FindFirstObjectByType<FirePropagationSystem>();
            }

            if (_suppressionController == null)
            {
                _suppressionController = FindFirstObjectByType<SuppressionRaycastController>();
            }

            if (_integrityController == null)
            {
                _integrityController = FindFirstObjectByType<FireObjectIntegrityController>();
            }

            if (_performanceProfiler == null)
            {
                _performanceProfiler = FindFirstObjectByType<PerformanceProfiler>();
            }

            if (_deviceProfiler == null)
            {
                _deviceProfiler = FindFirstObjectByType<DeviceProfiler>();
            }

            if (_agentSuppressionManager == null)
            {
                _agentSuppressionManager = FindFirstObjectByType<AgentSuppressionManager>();
            }
        }

        private void OnEnable()
        {
            if (_agentSuppressionManager != null)
            {
                _agentSuppressionManager.AgentConsumed += HandleAgentApplied;
            }
            else if (_suppressionController != null)
            {
                _suppressionController.AgentApplied += HandleAgentApplied;
            }
        }

        private void OnDisable()
        {
            if (_agentSuppressionManager != null)
            {
                _agentSuppressionManager.AgentConsumed -= HandleAgentApplied;
            }
            else if (_suppressionController != null)
            {
                _suppressionController.AgentApplied -= HandleAgentApplied;
            }
        }

        /// <summary>
        /// Starts a new metrics session for the configured training scenario.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when another session is already active.</exception>
        public void BeginSession()
        {
            if (_isSessionActive)
            {
                throw new InvalidOperationException("A training session is already active.");
            }

            _sessionMetrics = new SessionMetrics
            {
                ScenarioId = _scenarioId,
                StartedAtUtc = DateTime.UtcNow.ToString("O"),
                ObjectIntegrity = 1f
            };
            _sessionStartedAtRealtime = Time.realtimeSinceStartup;
            _performanceProfiler?.ResetSamples();
            _isSessionActive = true;
            SessionStarted?.Invoke();
        }

        /// <summary>
        /// Finalizes the active session and writes its JSON report to persistent application storage.
        /// </summary>
        /// <returns>A task that completes after the JSON report has been written.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no training session is active.</exception>
        public async Task EndSessionAsync()
        {
            if (!_isSessionActive)
            {
                throw new InvalidOperationException("No active training session is available to end.");
            }

            _lastSessionDurationSeconds = Time.realtimeSinceStartup - _sessionStartedAtRealtime;
            _sessionMetrics.DurationSeconds = _lastSessionDurationSeconds;
            _sessionMetrics.FinalFireIntensity = _firePropagationSystem == null ? 0f : _firePropagationSystem.AverageIntensity;
            _sessionMetrics.ObjectIntegrity = _integrityController == null ? _sessionMetrics.ObjectIntegrity : _integrityController.CurrentIntegrity;
            _sessionMetrics.IsFireSuppressed = _sessionMetrics.FinalFireIntensity <= _suppressedIntensityThreshold;
            _sessionMetrics.AverageFramesPerSecond = _performanceProfiler == null ? 0f : _performanceProfiler.AverageFramesPerSecond;
            _sessionMetrics.MinimumFramesPerSecond = _performanceProfiler == null ? 0f : _performanceProfiler.MinimumFramesPerSecond;
            _sessionMetrics.DeviceProfile = _deviceProfiler == null ? null : _deviceProfiler.CurrentProfile;
            _isSessionActive = false;
            SessionEnded?.Invoke(_sessionMetrics);

            string reportsDirectory = Path.Combine(Application.persistentDataPath, "SessionReports");
            Directory.CreateDirectory(reportsDirectory);
            string reportName = "session_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json";
            string reportPath = Path.Combine(reportsDirectory, reportName);
            string json = JsonUtility.ToJson(_sessionMetrics, true);
            await File.WriteAllTextAsync(reportPath, json);
            Debug.Log("MR Fire Safety: session report saved to " + reportPath);
        }

        private void HandleAgentApplied(float appliedAmount)
        {
            if (_isSessionActive)
            {
                _sessionMetrics.AgentConsumed += appliedAmount;
            }
        }
    }
}
