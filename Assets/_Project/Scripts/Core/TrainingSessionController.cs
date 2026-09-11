using System;
using MRFireSafety.Analytics.Systems;
using MRFireSafety.Core.Spatial;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
using MRFireSafety.Suppression.Controllers;
using UnityEngine;

namespace MRFireSafety.Core
{
    /// <summary>
    /// Owns the lifecycle of a training run. The session starts only after the virtual prop has been
    /// anchored on the physical floor, so that room scanning is excluded from the measured duration,
    /// and ends when the fire is suppressed below a configurable threshold or the protected object
    /// is destroyed.
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

        [Header("Completion")]
        [SerializeField, Range(0f, 1f)] private float _activeFireThreshold = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _suppressedFireThreshold = 0.025f;

        private bool _hasObservedActiveFire;
        private bool _isCompletingSession;

        /// <summary>
        /// Raised when a training run begins and the fire has been ignited.
        /// </summary>
        public event Action SessionStarted;

        /// <summary>
        /// Raised when the fire is reduced below the configured suppressed threshold.
        /// </summary>
        public event Action FireSuppressed;

        /// <summary>
        /// Gets whether a training run is currently in progress.
        /// </summary>
        public bool IsSessionRunning => _sessionDataManager != null && _sessionDataManager.IsSessionActive;

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

            if (_propPlacementController != null)
            {
                _propPlacementController.PropPlaced += HandlePropPlaced;
            }
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

            if (_propPlacementController != null)
            {
                _propPlacementController.PropPlaced -= HandlePropPlaced;
            }
        }

        private void Start()
        {
            if (_propPlacementController != null || !_startsWithoutPlacement)
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
            CompleteSession();
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
            CompleteSession();
        }

        private void CompleteSession()
        {
            if (_isCompletingSession || _sessionDataManager == null || !_sessionDataManager.IsSessionActive)
            {
                return;
            }

            _isCompletingSession = true;
            CompleteSessionAsync();
        }

        private async void CompleteSessionAsync()
        {
            try
            {
                await _sessionDataManager.EndSessionAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _isCompletingSession = false;
            }
        }
    }
}
