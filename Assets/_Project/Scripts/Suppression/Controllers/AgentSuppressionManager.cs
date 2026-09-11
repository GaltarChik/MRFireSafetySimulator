using System;
using MRFireSafety.Suppression.Handlers;
using UnityEngine;

namespace MRFireSafety.Suppression.Controllers
{
    /// <summary>
    /// Tracks virtual extinguisher-agent capacity and aggregates agent use reported by the raycast
    /// and particle suppression paths. The manager owns which mechanism is authoritative and gates
    /// every suppression source when the reservoir runs empty.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentSuppressionManager : MonoBehaviour
    {
        [SerializeField] private SuppressionRaycastController _raycastController;
        [SerializeField] private ParticleCollisionHandler _particleCollisionHandler;
        [SerializeField] private SuppressionSourceMode _suppressionSourceMode = SuppressionSourceMode.RaycastOnly;
        [SerializeField, Min(0.01f)] private float _maximumAgentCapacity = 10f;

        private float _remainingAgentCapacity;

        /// <summary>
        /// Raised after virtual extinguishing agent is consumed, carrying the consumed amount.
        /// </summary>
        public event Action<float> AgentConsumed;

        /// <summary>
        /// Raised when the virtual extinguisher becomes empty.
        /// </summary>
        public event Action AgentDepleted;

        /// <summary>
        /// Gets the agent amount remaining in the virtual extinguisher.
        /// </summary>
        public float RemainingAgentCapacity => _remainingAgentCapacity;

        /// <summary>
        /// Gets the configured maximum virtual extinguishing-agent capacity.
        /// </summary>
        public float MaximumAgentCapacity => _maximumAgentCapacity;

        /// <summary>
        /// Gets whether the virtual extinguisher has agent remaining.
        /// </summary>
        public bool HasAgentRemaining => _remainingAgentCapacity > 0f;

        /// <summary>
        /// Gets the suppression mechanism that is authoritative for this extinguisher.
        /// </summary>
        public SuppressionSourceMode SourceMode => _suppressionSourceMode;

        private void Awake()
        {
            _remainingAgentCapacity = _maximumAgentCapacity;
            if (_raycastController == null)
            {
                _raycastController = GetComponent<SuppressionRaycastController>();
            }

            if (_particleCollisionHandler == null)
            {
                _particleCollisionHandler = GetComponentInChildren<ParticleCollisionHandler>();
            }

            ApplySourceMode();
        }

        private void OnEnable()
        {
            if (_raycastController != null)
            {
                _raycastController.AgentApplied += HandleAgentApplied;
            }

            if (_particleCollisionHandler != null)
            {
                _particleCollisionHandler.AgentApplied += HandleAgentApplied;
            }
        }

        private void OnDisable()
        {
            if (_raycastController != null)
            {
                _raycastController.AgentApplied -= HandleAgentApplied;
            }

            if (_particleCollisionHandler != null)
            {
                _particleCollisionHandler.AgentApplied -= HandleAgentApplied;
            }
        }

        /// <summary>
        /// Refills the virtual extinguisher to its configured maximum capacity and re-enables the
        /// suppression sources selected by the current mode.
        /// </summary>
        public void RefillAgent()
        {
            _remainingAgentCapacity = _maximumAgentCapacity;
            ApplySourceMode();
        }

        private void ApplySourceMode()
        {
            bool isRaycastAuthoritative = _suppressionSourceMode != SuppressionSourceMode.ParticleOnly;
            bool isParticleAuthoritative = _suppressionSourceMode != SuppressionSourceMode.RaycastOnly;
            if (_raycastController != null)
            {
                _raycastController.SetSprayEnabled(HasAgentRemaining);
                _raycastController.SetSuppressionEnabled(isRaycastAuthoritative && HasAgentRemaining);
            }

            _particleCollisionHandler?.SetSuppressionEnabled(isParticleAuthoritative && HasAgentRemaining);
        }

        private void HandleAgentApplied(float requestedAmount)
        {
            if (!HasAgentRemaining || requestedAmount <= 0f)
            {
                return;
            }

            float consumedAmount = Mathf.Min(requestedAmount, _remainingAgentCapacity);
            _remainingAgentCapacity -= consumedAmount;
            AgentConsumed?.Invoke(consumedAmount);

            if (HasAgentRemaining)
            {
                return;
            }

            ApplySourceMode();
            AgentDepleted?.Invoke();
        }
    }
}
