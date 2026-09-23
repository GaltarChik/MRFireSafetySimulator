using System;
using MRFireSafety.Fire.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MRFireSafety.Suppression.Controllers
{
    /// <summary>
    /// Discharges the virtual extinguisher while its configured OpenXR input action is held and
    /// casts a lightweight ray from the nozzle to find the fire. Agent is consumed by pulling the
    /// trigger, not by hitting the target, which is what makes aim quality measurable: a trainee who
    /// sprays at nothing empties the extinguisher exactly as they would in reality. Suppression
    /// itself requires a hit, and needs no rigidbodies or per-particle collision checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuppressionRaycastController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionProperty _sprayAction;

        [Header("Raycast")]
        [SerializeField] private Transform _nozzleTransform;
        [SerializeField, Min(0.1f)] private float _maximumDistance = 5f;
        [SerializeField, Min(0.01f)] private float _suppressionRadius = 0.3f;
        [Tooltip("Agent discharged per second while the trigger is held. The same amount is removed from the fire when the ray connects.")]
        [SerializeField, Min(0.01f)] private float _agentPerSecond = 0.5f;
        [SerializeField] private LayerMask _targetLayers = Physics.DefaultRaycastLayers;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem _agentParticleSystem;

        /// <summary>
        /// Raised for every amount of agent that leaves the extinguisher, whether or not it reaches
        /// the fire. This is the authoritative source of agent consumption.
        /// </summary>
        public event Action<float> AgentDischarged;

        /// <summary>
        /// Raised when discharged agent actually reaches a fire source.
        /// </summary>
        public event Action<float> AgentOnTarget;

        /// <summary>
        /// Raised when the trainee starts or stops pressing the spray control.
        /// </summary>
        public event Action<bool> SprayingChanged;

        /// <summary>
        /// Gets whether the extinguisher may discharge at all. The agent manager clears this when
        /// the reservoir is empty.
        /// </summary>
        public bool IsSprayEnabled { get; private set; } = true;

        /// <summary>
        /// Gets whether this controller is the authoritative suppression mechanism.
        /// </summary>
        public bool IsSuppressionEnabled { get; private set; } = true;

        /// <summary>
        /// Gets whether the trainee is currently discharging the extinguisher.
        /// </summary>
        public bool IsSpraying { get; private set; }

        /// <summary>
        /// Assigns the input, nozzle, and feedback references without relying on editor-only
        /// serialized property access, so that scene generation can wire the rig deterministically.
        /// </summary>
        /// <param name="sprayAction">Action held to discharge extinguishing agent.</param>
        /// <param name="nozzleTransform">Transform whose forward direction the ray follows.</param>
        /// <param name="agentParticleSystem">Particle stream shown while spraying.</param>
        /// <param name="targetLayers">Layers the suppression ray is allowed to hit.</param>
        public void Configure(InputActionProperty sprayAction, Transform nozzleTransform, ParticleSystem agentParticleSystem, LayerMask targetLayers)
        {
            _sprayAction = sprayAction;
            _nozzleTransform = nozzleTransform;
            _agentParticleSystem = agentParticleSystem;
            _targetLayers = targetLayers;
        }

        /// <summary>
        /// Discharges one interval worth of agent and applies it to the fire when the nozzle ray
        /// connects. Agent is reported as consumed regardless of whether the ray finds a target.
        /// </summary>
        /// <param name="deltaTimeSeconds">Elapsed time to discharge for, in seconds.</param>
        /// <returns>True when the discharged agent reached a fire source; otherwise false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elapsed time is negative.</exception>
        public bool DischargeAgent(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds), "Elapsed time cannot be negative.");
            }

            if (deltaTimeSeconds <= 0f)
            {
                return false;
            }

            float dischargedAmount = _agentPerSecond * deltaTimeSeconds;
            AgentDischarged?.Invoke(dischargedAmount);

            if (!IsSuppressionEnabled || _nozzleTransform == null)
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
            firePropagationSystem.ApplySuppression(localHitPosition, _suppressionRadius, dischargedAmount);
            AgentOnTarget?.Invoke(dischargedAmount);
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
        /// Enables or disables the extinguisher discharge, including its visual effect. The agent
        /// manager disables it when the reservoir is empty.
        /// </summary>
        /// <param name="isEnabled">True to allow discharge; otherwise false.</param>
        public void SetSprayEnabled(bool isEnabled)
        {
            IsSprayEnabled = isEnabled;
            if (!isEnabled)
            {
                SetSpraying(false);
            }
        }

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
            if (!isSpraying)
            {
                return;
            }

            DischargeAgent(Time.deltaTime);
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
