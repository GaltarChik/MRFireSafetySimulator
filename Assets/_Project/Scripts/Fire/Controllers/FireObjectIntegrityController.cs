using System;
using MRFireSafety.Fire.Systems;
using UnityEngine;

namespace MRFireSafety.Fire.Controllers
{
    /// <summary>
    /// Converts sustained fire intensity into normalized damage to the protected virtual object.
    /// Damage accumulates against simulated time, so the measured integrity of the object depends
    /// on how long the fire burns rather than on how often the trainee applies extinguishing agent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FireObjectIntegrityController : MonoBehaviour
    {
        [SerializeField] private FirePropagationSystem _firePropagationSystem;
        [SerializeField, Range(0f, 1f)] private float _initialIntegrity = 1f;
        [Tooltip("Integrity removed each second while the average fire intensity is at its maximum.")]
        [SerializeField, Min(0f)] private float _damagePerIntensitySecond = 0.025f;

        private float _currentIntegrity;

        /// <summary>
        /// Raised when the normalized integrity of the protected object changes.
        /// </summary>
        public event Action<float> IntegrityChanged;

        /// <summary>
        /// Raised once when the integrity of the protected object reaches zero.
        /// </summary>
        public event Action ObjectDestroyed;

        /// <summary>
        /// Gets the current normalized integrity in the inclusive range from zero to one.
        /// </summary>
        public float CurrentIntegrity => _currentIntegrity;

        /// <summary>
        /// Gets whether the protected virtual object has been fully damaged.
        /// </summary>
        public bool IsDestroyed => _currentIntegrity <= 0f;

        private void Awake()
        {
            _currentIntegrity = _initialIntegrity;
            if (_firePropagationSystem == null)
            {
                _firePropagationSystem = GetComponentInChildren<FirePropagationSystem>();
            }
        }

        private void OnEnable()
        {
            if (_firePropagationSystem != null)
            {
                _firePropagationSystem.SimulationStepped += HandleSimulationStepped;
            }
        }

        private void OnDisable()
        {
            if (_firePropagationSystem != null)
            {
                _firePropagationSystem.SimulationStepped -= HandleSimulationStepped;
            }
        }

        /// <summary>
        /// Restores integrity to the initial configured value and notifies presentation clients.
        /// </summary>
        public void ResetIntegrity()
        {
            _currentIntegrity = _initialIntegrity;
            IntegrityChanged?.Invoke(_currentIntegrity);
        }

        private void HandleSimulationStepped(FireSimulationStep simulationStep)
        {
            if (IsDestroyed || simulationStep.AverageIntensity <= 0f)
            {
                return;
            }

            float previousIntegrity = _currentIntegrity;
            float damage = simulationStep.AverageIntensity * _damagePerIntensitySecond * simulationStep.DeltaTimeSeconds;
            _currentIntegrity = Mathf.Clamp01(_currentIntegrity - damage);
            if (!Mathf.Approximately(previousIntegrity, _currentIntegrity))
            {
                IntegrityChanged?.Invoke(_currentIntegrity);
            }

            if (previousIntegrity > 0f && IsDestroyed)
            {
                ObjectDestroyed?.Invoke();
            }
        }
    }
}
