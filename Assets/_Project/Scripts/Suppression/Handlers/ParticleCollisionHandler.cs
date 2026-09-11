using System;
using System.Collections.Generic;
using MRFireSafety.Fire.Systems;
using UnityEngine;

namespace MRFireSafety.Suppression.Handlers
{
    /// <summary>
    /// Applies extinguishing agent when a configured low-rate particle stream hits a trigger collider.
    /// The collision-event buffer is allocated once and reused to avoid garbage collection during play,
    /// and the handler stays inert unless the agent manager has made it an authoritative source with
    /// agent left in the reservoir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleCollisionHandler : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float _suppressionPerCollision = 0.015f;
        [SerializeField, Min(0.01f)] private float _suppressionRadius = 0.12f;
        [SerializeField, Min(1)] private int _maximumCollisionEventsPerFrame = 12;

        private readonly List<ParticleCollisionEvent> _collisionEvents = new List<ParticleCollisionEvent>(16);
        private ParticleSystem _agentParticleSystem;

        /// <summary>
        /// Raised after a particle collision applies agent to a fire source.
        /// </summary>
        public event Action<float> AgentApplied;

        /// <summary>
        /// Gets whether particle collisions currently reduce fire intensity. The agent manager
        /// clears this when another mechanism is authoritative or the reservoir is empty.
        /// </summary>
        public bool IsSuppressionEnabled { get; private set; } = true;

        /// <summary>
        /// Enables or disables particle-driven extinguishing agent application.
        /// </summary>
        /// <param name="isEnabled">True to allow application; otherwise false.</param>
        public void SetSuppressionEnabled(bool isEnabled)
        {
            IsSuppressionEnabled = isEnabled;
        }

        private void Awake()
        {
            _agentParticleSystem = GetComponent<ParticleSystem>();
        }

        private void OnParticleCollision(GameObject otherObject)
        {
            if (!IsSuppressionEnabled)
            {
                return;
            }

            FirePropagationSystem firePropagationSystem = otherObject.GetComponentInParent<FirePropagationSystem>();
            if (firePropagationSystem == null)
            {
                firePropagationSystem = otherObject.GetComponentInChildren<FirePropagationSystem>();
            }

            if (firePropagationSystem == null || !firePropagationSystem.IsInitialized)
            {
                return;
            }

            int eventCount = ParticlePhysicsExtensions.GetCollisionEvents(_agentParticleSystem, otherObject, _collisionEvents);
            int appliedEventCount = Mathf.Min(eventCount, _maximumCollisionEventsPerFrame);
            for (int eventIndex = 0; eventIndex < appliedEventCount; eventIndex++)
            {
                Vector3 localImpactPoint = firePropagationSystem.transform.InverseTransformPoint(_collisionEvents[eventIndex].intersection);
                firePropagationSystem.ApplySuppression(localImpactPoint, _suppressionRadius, _suppressionPerCollision);
                AgentApplied?.Invoke(_suppressionPerCollision);
            }
        }
    }
}
