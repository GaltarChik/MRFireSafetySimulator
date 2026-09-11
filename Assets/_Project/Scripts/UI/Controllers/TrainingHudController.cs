using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Systems;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
using MRFireSafety.Suppression.Controllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MRFireSafety.UI.Controllers
{
    /// <summary>
    /// Displays concise in-session training data: elapsed time, extinguisher capacity, fire level,
    /// and protected-object integrity. Values are refreshed from system events, and the timer text
    /// is rewritten only when the displayed second changes, so the interface adds no measurable
    /// per-frame cost to the mobile XR frame budget.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingHudController : MonoBehaviour
    {
        [SerializeField] private FirePropagationSystem _firePropagationSystem;
        [SerializeField] private FireObjectIntegrityController _integrityController;
        [SerializeField] private AgentSuppressionManager _agentSuppressionManager;
        [SerializeField] private SessionDataManager _sessionDataManager;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Slider _agentSlider;
        [SerializeField] private Slider _fireSlider;
        [SerializeField] private Slider _integritySlider;
        [SerializeField, Range(0f, 1f)] private float _suppressedFireThreshold = 0.025f;

        private float _maximumAgentCapacity = 1f;
        private int _displayedSeconds = -1;

        /// <summary>
        /// Assigns generated HUD controls without relying on editor-only serialized-property access.
        /// </summary>
        /// <param name="timerText">Text used for elapsed time.</param>
        /// <param name="statusText">Text used for the training status.</param>
        /// <param name="agentSlider">Slider used for agent capacity.</param>
        /// <param name="fireSlider">Slider used for fire intensity.</param>
        /// <param name="integritySlider">Slider used for protected-object integrity.</param>
        public void Configure(TMP_Text timerText, TMP_Text statusText, Slider agentSlider, Slider fireSlider, Slider integritySlider)
        {
            _timerText = timerText;
            _statusText = statusText;
            _agentSlider = agentSlider;
            _fireSlider = fireSlider;
            _integritySlider = integritySlider;
        }

        private void Awake()
        {
            _firePropagationSystem ??= FindFirstObjectByType<FirePropagationSystem>();
            _integrityController ??= FindFirstObjectByType<FireObjectIntegrityController>();
            _agentSuppressionManager ??= FindFirstObjectByType<AgentSuppressionManager>();
            _sessionDataManager ??= FindFirstObjectByType<SessionDataManager>();
            _maximumAgentCapacity = _agentSuppressionManager == null ? 1f : Mathf.Max(0.001f, _agentSuppressionManager.MaximumAgentCapacity);
        }

        private void OnEnable()
        {
            if (_firePropagationSystem != null)
            {
                _firePropagationSystem.AverageIntensityChanged += HandleFireIntensityChanged;
            }

            if (_integrityController != null)
            {
                _integrityController.IntegrityChanged += HandleIntegrityChanged;
            }

            if (_agentSuppressionManager != null)
            {
                _agentSuppressionManager.AgentConsumed += HandleAgentConsumed;
            }

            if (_sessionDataManager != null)
            {
                _sessionDataManager.SessionStarted += HandleSessionStarted;
                _sessionDataManager.SessionEnded += HandleSessionEnded;
            }
        }

        private void OnDisable()
        {
            if (_firePropagationSystem != null)
            {
                _firePropagationSystem.AverageIntensityChanged -= HandleFireIntensityChanged;
            }

            if (_integrityController != null)
            {
                _integrityController.IntegrityChanged -= HandleIntegrityChanged;
            }

            if (_agentSuppressionManager != null)
            {
                _agentSuppressionManager.AgentConsumed -= HandleAgentConsumed;
            }

            if (_sessionDataManager != null)
            {
                _sessionDataManager.SessionStarted -= HandleSessionStarted;
                _sessionDataManager.SessionEnded -= HandleSessionEnded;
            }
        }

        private void Start()
        {
            HandleFireIntensityChanged(_firePropagationSystem == null ? 0f : _firePropagationSystem.AverageIntensity);
            HandleIntegrityChanged(_integrityController == null ? 1f : _integrityController.CurrentIntegrity);
            RefreshAgentSlider();
            UpdateTimerText(0);

            bool isWaitingForSession = _sessionDataManager != null && !_sessionDataManager.IsSessionActive;
            if (isWaitingForSession && _statusText != null)
            {
                _statusText.text = "STATUS  PLACE THE TRAINING PROP";
            }
        }

        private void Update()
        {
            if (_sessionDataManager == null || !_sessionDataManager.IsSessionActive || _timerText == null)
            {
                return;
            }

            int fullSeconds = Mathf.FloorToInt(_sessionDataManager.ElapsedSessionSeconds);
            if (fullSeconds != _displayedSeconds)
            {
                UpdateTimerText(fullSeconds);
            }
        }

        private void HandleSessionStarted()
        {
            _displayedSeconds = -1;
            UpdateTimerText(0);
            RefreshAgentSlider();
        }

        private void HandleSessionEnded(SessionMetrics metrics)
        {
            UpdateTimerText(Mathf.FloorToInt(metrics.DurationSeconds));
            if (_statusText != null)
            {
                _statusText.text = metrics.IsFireSuppressed ? "STATUS  FIRE SUPPRESSED" : "STATUS  SESSION ENDED";
            }
        }

        private void HandleFireIntensityChanged(float averageIntensity)
        {
            if (_fireSlider != null)
            {
                _fireSlider.SetValueWithoutNotify(averageIntensity);
            }

            if (_statusText != null && (_sessionDataManager == null || _sessionDataManager.IsSessionActive))
            {
                _statusText.text = averageIntensity <= _suppressedFireThreshold
                    ? "STATUS  FIRE SUPPRESSED"
                    : "STATUS  EXTINGUISH THE SOURCE";
            }
        }

        private void HandleIntegrityChanged(float integrity)
        {
            if (_integritySlider != null)
            {
                _integritySlider.SetValueWithoutNotify(integrity);
            }
        }

        private void HandleAgentConsumed(float consumedAmount)
        {
            RefreshAgentSlider();
        }

        private void RefreshAgentSlider()
        {
            if (_agentSlider == null)
            {
                return;
            }

            float remainingAgent = _agentSuppressionManager == null
                ? _maximumAgentCapacity
                : _agentSuppressionManager.RemainingAgentCapacity;
            _agentSlider.SetValueWithoutNotify(remainingAgent / _maximumAgentCapacity);
        }

        private void UpdateTimerText(int fullSeconds)
        {
            _displayedSeconds = fullSeconds;
            if (_timerText != null)
            {
                _timerText.text = $"TIME  {fullSeconds / 60:00}:{fullSeconds % 60:00}";
            }
        }
    }
}
