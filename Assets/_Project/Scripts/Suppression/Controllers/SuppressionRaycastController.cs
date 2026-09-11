using System;
using MRFireSafety.Fire.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MRFireSafety.Suppression.Controllers
{
    /// <summary>
    /// Casts a lightweight ray from an extinguisher nozzle while its configured OpenXR input action
    /// is held. A successful hit suppresses the target fire grid without requiring rigidbodies or
    /// per-particle collision checks. Spraying and suppressing are gated separately so that the
    /// agent stream can remain a purely visual effect when another mechanism is authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuppressionRaycastController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionProperty _sprayAction;

        [Header("Raycast")]
        [SerializeField] private Transform _nozzleTransform;
        [SerializeField, Min(0.1f)] private float _maximumDistance = 5f;
        [SerializeField, Min(0.01f)] private float _suppressionRadius = 0.22f;
        [SerializeField, Min(0.01f)] private float _suppressionPerSecond = 0.5f;
        [SerializeField] private LayerMask _targetLayers = Physics.DefaultRaycastLayers;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem _agentParticleSystem;

        /// <summary>
        /// Raised when this controller applies extinguishing agent to a fire source.
        /// </summary>
        public event Action<float> AgentApplied;

        /// <summary>
        /// Raised when the trainee starts or stops pressing the spray control.
        /// </summary>
        public event Action<bool> SprayingChanged;

        /// <summary>
        /// Gets whether the extinguisher may emit agent at all. The agent manager clears this when
        /// the reservoir is empty.
        /// </summary>
        public bool IsSprayEnabled { get; private set; } = true;

        /// <summary>
        /// Gets whether this controller is the authoritative suppression mechanism.
        /// </summary>
        public bool IsSuppressionEnabled { get; private set; } = true;

        /// <summary>
        /// Gets whether the trainee is currently spraying.
        /// </summary>
        public bool IsSpraying { get; private set; }

        private void OnEnable()
        {
            if (_sprayAction.action != null)
            {
                _sprayAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_sprayAction.action != null)
            {
                _sprayAction.action.Disable();
            }

            SetSpraying(false);
        }

        private void Update()
        {
            bool isSpraying = IsSprayEnabled && _sprayAction.action != null && _sprayAction.action.IsPressed();
            SetSpraying(isSpraying);
            if (!isSpraying || !IsSuppressionEnabled)
            {
                return;
            }

            ApplySuppression(Time.deltaTime);
        }

        /// <summary>
        /// Attempts to apply one amount of extinguishing agent along the forward direction of the nozzle.
        /// </summary>
        /// <param name="deltaTime">Elapsed time since the previous application attempt.</param>
        /// <returns>True when a fire source was hit and reduced; otherwise false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when deltaTime is negative.</exception>
        public bool ApplySuppression(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
            }

            if (!IsSuppressionEnabled || _nozzleTransform == null || deltaTime <= 0f)
            {
                return false;
            }

            if (!Physics.Raycast(_nozzleTransform.position, _nozzleTransform.forward, out RaycastHit hit, _maximumDistance, _targetLayers, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            FirePropagationSystem firePropagationSystem = hit.collider.GetComponentInParent<FirePropagationSystem>();
            if (firePropagationSystem == null)
            {
                firePropagationSystem = hit.collider.GetComponentInChildren<FirePropagationSystem>();
            }

            if (firePropagationSystem == null || !firePropagationSystem.IsInitialized)
            {
                return false;
            }

            Vector3 localHitPosition = firePropagationSystem.transform.InverseTransformPoint(hit.point);
            float appliedAmount = _suppressionPerSecond * deltaTime;
            firePropagationSystem.ApplySuppression(localHitPosition, _suppressionRadius, appliedAmount);
            AgentApplied?.Invoke(appliedAmount);
            return true;
        }

        /// <summary>
        /// Enables or disables raycast-based extinguishing agent application.
        /// </summary>
        /// <param name="isEnabled">True to allow application; otherwise false.</param>
        public void SetSuppressionEnabled(bool isEnabled)
        {
            IsSuppressionEnabled = isEnabled;
        }

        /// <summary>
        /// Enables or disables the extinguisher stream, including its visual effect. The agent
        /// manager disables it when the reservoir is empty.
        /// </summary>
        /// <param name="isEnabled">True to allow spraying; otherwise false.</param>
        public void SetSprayEnabled(bool isEnabled)
        {
            IsSprayEnabled = isEnabled;
            if (!isEnabled)
            {
                SetSpraying(false);
            }
        }

        private void SetSpraying(bool isSpraying)
        {
            if (IsSpraying == isSpraying)
            {
                return;
            }

            IsSpraying = isSpraying;
            UpdateAgentVisual(isSpraying);
            SprayingChanged?.Invoke(isSpraying);
        }

        private void UpdateAgentVisual(bool isSpraying)
        {
            if (_agentParticleSystem == null)
            {
                return;
            }

            if (isSpraying)
            {
                _agentParticleSystem.Play();
                return;
            }

            _agentParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
