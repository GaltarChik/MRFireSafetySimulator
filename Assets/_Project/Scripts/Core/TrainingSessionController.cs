using System;
using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Systems;
using MRFireSafety.Core.Spatial;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
using MRFireSafety.Suppression.Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MRFireSafety.Core
{
    /// <summary>
    /// Owns the lifecycle of a training run. The session starts only after the virtual prop has been
    /// anchored on the physical floor, so that room scanning is excluded from the measured duration.
    /// It ends when the fire is suppressed, when the extinguisher runs dry, or when the protected
    /// object is destroyed. A controller button starts the next run, which lets an evaluator collect
    /// repeated sessions without restarting the application.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingSessionController : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private FirePropagationSystem _firePropagationSystem;
        [SerializeField] private FireObjectIntegrityController _integrityController;
        [SerializeField] private AgentSuppressionManager _agentSuppressionManager;
        [SerializeField] private SessionDataManager _sessionDataManager;

        [Header("Start trigger")]
        [SerializeField] private VirtualPropPlacementController _propPlacementController;
        [Tooltip("Editor preview only: starts the session immediately when no placement controller is assigned.")]
        [SerializeField] private bool _startsWithoutPlacement = true;

        [Header("Restart")]
        [SerializeField] private InputActionProperty _restartAction;
        [Tooltip("Time after a completed run during which the restart control is ignored, so that a held button does not skip the results.")]
        [SerializeField, Min(0f)] private float _restartLockoutSeconds = 1.5f;

        [Header("Completion")]
        [SerializeField, Range(0f, 1f)] private float _activeFireThreshold = 0.012f;
        [SerializeField, Range(0f, 1f)] private float _suppressedFireThreshold = 0.006f;

        private bool _hasObservedActiveFire;
        private bool _isCompletingSession;
        private float _sessionCompletedAtRealtime;

        /// <summary>
        /// Raised when a training run begins and the fire has been ignited.
        /// </summary>
        public event Action SessionStarted;

        /// <summary>
        /// Raised when the fire is reduced below the configured suppressed threshold.
        /// </summary>
        public event Action FireSuppressed;

        /// <summary>
        /// Raised when a run finishes, carrying the reason it ended.
        /// </summary>
        public event Action<SessionOutcome> SessionCompleted;

        /// <summary>
        /// Gets whether a training run is currently in progress.
        /// </summary>
        public bool IsSessionRunning => _sessionDataManager != null && _sessionDataManager.IsSessionActive;

        /// <summary>
        /// Gets whether the restart control is currently accepted.
        /// </summary>
        public bool CanRestart => !IsSessionRunning
            && !_isCompletingSession
            && Time.realtimeSinceStartup - _sessionCompletedAtRealtime >= _restartLockoutSeconds;

        /// <summary>
        /// Assigns the restart control without relying on editor-only serialized property access,
        /// so that scene generation can wire it deterministically.
        /// </summary>
        /// <param name="restartAction">Action pressed to begin the next training run.</param>
        public void ConfigureRestartInput(InputActionProperty restartAction)
        {
            _restartAction = restartAction;
        }

        /// <summary>
        /// Starts a training run: the fire grid is ignited, the protected object and extinguisher
        /// are reset, and metrics collection begins.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no fire propagation system is available.</exception>
        public void StartSession()
        {
            if (_firePropagationSystem == null)
            {
                throw new InvalidOperationException("A fire propagation system is required to start a training session.");
            }

            if (IsSessionRunning)
            {
                return;
            }

            _hasObservedActiveFire = false;
            _isCompletingSession = false;
            _integrityController?.ResetIntegrity();
            _agentSuppressionManager?.RefillAgent();
            _firePropagationSystem.InitializeFire();
            _sessionDataManager?.BeginSession();
            SessionStarted?.Invoke();
        }

        private void Awake()
        {
            if (_firePropagationSystem == null)
            {
                _firePropagationSystem = FindFirstObjectByType<FirePropagationSystem>();
            }

            if (_integrityController == null)
            {
                _integrityController = FindFirstObjectByType<FireObjectIntegrityController>();
            }

            if (_agentSuppressionManager == null)
            {
                _agentSuppressionManager = FindFirstObjectByType<AgentSuppressionManager>();
            }

            if (_sessionDataManager == null)
            {
                _sessionDataManager = GetComponent<SessionDataManager>();
            }

            if (_propPlacementController == null)
            {
                _propPlacementController = FindFirstObjectByType<VirtualPropPlacementController>();
            }
        }

        private void OnEnable()
        {
            if (_firePropagationSystem != null)
            {
                _firePropagationSystem.AverageIntensityChanged += HandleAverageIntensityChanged;
            }

            if (_integrityController != null)
            {
                _integrityController.ObjectDestroyed += HandleObjectDestroyed;
            }

            if (_agentSuppressionManager != null)
            {
                _agentSuppressionManager.AgentDepleted += HandleAgentDepleted;
            }

            if (_propPlacementController != null)
            {
                _propPlacementController.PropPlaced += HandlePropPlaced;
            }

            _restartAction.action?.Enable();
        }

        private void OnDisable()
        {
            if (_firePropagationSystem != null)
            {
                _firePropagationSystem.AverageIntensityChanged -= HandleAverageIntensityChanged;
            }

            if (_integrityController != null)
            {
                _integrityController.ObjectDestroyed -= HandleObjectDestroyed;
            }

            if (_agentSuppressionManager != null)
            {
                _agentSuppressionManager.AgentDepleted -= HandleAgentDepleted;
            }

            if (_propPlacementController != null)
            {
                _propPlacementController.PropPlaced -= HandlePropPlaced;
            }

            _restartAction.action?.Disable();
        }

        private void Start()
        {
            if (_propPlacementController != null || !_startsWithoutPlacement)
            {
                return;
            }

            StartSession();
        }

        private void Update()
        {
            if (_restartAction.action == null || !_restartAction.action.WasPressedThisFrame() || !CanRestart)
            {
                return;
            }

            StartSession();
        }

        private void HandlePropPlaced(Pose placementPose)
        {
            StartSession();
        }

        private void HandleObjectDestroyed()
        {
            CompleteSession(SessionOutcome.ObjectDestroyed);
        }

        private void HandleAgentDepleted()
        {
            // An empty extinguisher only ends the run while the fire is still burning; otherwise the
            // suppression path below reports the successful outcome.
            if (_firePropagationSystem != null && _firePropagationSystem.AverageIntensity > _suppressedFireThreshold)
            {
                CompleteSession(SessionOutcome.AgentDepleted);
            }
        }

        private void HandleAverageIntensityChanged(float averageIntensity)
        {
            if (averageIntensity >= _activeFireThreshold)
            {
                _hasObservedActiveFire = true;
            }

            if (_isCompletingSession || !_hasObservedActiveFire || averageIntensity > _suppressedFireThreshold)
            {
                return;
            }

            FireSuppressed?.Invoke();
            CompleteSession(SessionOutcome.Suppressed);
        }

        private void CompleteSession(SessionOutcome outcome)
        {
            if (_isCompletingSession || _sessionDataManager == null || !_sessionDataManager.IsSessionActive)
            {
                return;
            }

            _isCompletingSession = true;
            CompleteSessionAsync(outcome);
        }

        private async void CompleteSessionAsync(SessionOutcome outcome)
        {
            try
            {
                await _sessionDataManager.EndSessionAsync(outcome);
                _sessionCompletedAtRealtime = Time.realtimeSinceStartup;
                SessionCompleted?.Invoke(outcome);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                _isCompletingSession = false;
            }
        }
    }
}
