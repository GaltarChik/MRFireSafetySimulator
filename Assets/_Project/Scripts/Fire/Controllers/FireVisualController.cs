using MRFireSafety.Fire.Systems;
using UnityEngine;

namespace MRFireSafety.Fire.Controllers
{
    /// <summary>
    /// Maps aggregate cellular fire intensity to lightweight particle and point-light visuals.
    /// It changes renderer parameters only when the simulation publishes an event rather than
    /// polling or allocating data every frame. Smoke trails the fire with a configurable lag so
    /// that a freshly extinguished source keeps smoking briefly, as a real one would.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirePropagationSystem))]
    public sealed class FireVisualController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _fireParticleSystem;
        [SerializeField] private ParticleSystem _smokeParticleSystem;
        [SerializeField] private Light _fireLight;
        [SerializeField, Min(0f)] private float _maximumEmissionRate = 40f;
        [SerializeField, Min(0f)] private float _maximumSmokeEmissionRate = 12f;
        [SerializeField, Min(0f)] private float _maximumLightIntensity = 3.5f;
        [Tooltip("Residual smoke that keeps rising after the fire is out, as a fraction of the maximum rate.")]
        [SerializeField, Range(0f, 1f)] private float _residualSmokeFraction = 0.25f;
        [Tooltip("Seconds the residual smoke keeps rising after the fire falls below the visible threshold.")]
        [SerializeField, Min(0f)] private float _residualSmokeSeconds = 4f;
        [SerializeField, Range(0f, 1f)] private float _visibleFireThreshold = 0.01f;

        private FirePropagationSystem _firePropagationSystem;
        private float _residualSmokeRemaining;
        private float _currentIntensity;

        /// <summary>
        /// Assigns the generated visual components without relying on editor-only serialized
        /// property access.
        /// </summary>
        /// <param name="fireParticleSystem">Flame particle system.</param>
        /// <param name="smokeParticleSystem">Smoke particle system, or null when unused.</param>
        /// <param name="fireLight">Point light tinted by the fire, or null when unused.</param>
        public void Configure(ParticleSystem fireParticleSystem, ParticleSystem smokeParticleSystem, Light fireLight)
        {
            _fireParticleSystem = fireParticleSystem;
            _smokeParticleSystem = smokeParticleSystem;
            _fireLight = fireLight;
        }

        private void Awake()
        {
            _firePropagationSystem = GetComponent<FirePropagationSystem>();
            if (_fireParticleSystem == null)
            {
                _fireParticleSystem = GetComponent<ParticleSystem>();
            }

            if (_fireLight == null)
            {
                _fireLight = GetComponent<Light>();
            }
        }

        private void OnEnable()
        {
            if (_firePropagationSystem == null)
            {
                _firePropagationSystem = GetComponent<FirePropagationSystem>();
            }

            _firePropagationSystem.AverageIntensityChanged += HandleAverageIntensityChanged;
        }

        private void OnDisable()
        {
            if (_firePropagationSystem != null)
            {
                _firePropagationSystem.AverageIntensityChanged -= HandleAverageIntensityChanged;
            }
        }

        private void Update()
        {
            if (_residualSmokeRemaining <= 0f)
            {
                return;
            }

            _residualSmokeRemaining -= Time.deltaTime;
            ApplySmokeEmission();
        }

        private void HandleAverageIntensityChanged(float averageIntensity)
        {
            _currentIntensity = averageIntensity;

            if (_fireParticleSystem != null)
            {
                ParticleSystem.EmissionModule emissionModule = _fireParticleSystem.emission;
                emissionModule.rateOverTime = _maximumEmissionRate * averageIntensity;
            }

            if (_fireLight != null)
            {
                _fireLight.intensity = _maximumLightIntensity * averageIntensity;
            }

            if (averageIntensity > _visibleFireThreshold)
            {
                _residualSmokeRemaining = _residualSmokeSeconds;
            }

            ApplySmokeEmission();
        }

        private void ApplySmokeEmission()
        {
            if (_smokeParticleSystem == null)
            {
                return;
            }

            float activeSmokeRate = _maximumSmokeEmissionRate * _currentIntensity;
            float residualSmokeRate = _residualSmokeRemaining > 0f
                ? _maximumSmokeEmissionRate * _residualSmokeFraction
                : 0f;

            ParticleSystem.EmissionModule emissionModule = _smokeParticleSystem.emission;
            emissionModule.rateOverTime = Mathf.Max(activeSmokeRate, residualSmokeRate);
        }
    }
}
