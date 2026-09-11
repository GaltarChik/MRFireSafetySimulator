using MRFireSafety.Fire.Systems;
using UnityEngine;

namespace MRFireSafety.Fire.Controllers
{
    /// <summary>
    /// Maps aggregate cellular fire intensity to lightweight particle and point-light visuals.
    /// It changes renderer parameters only when the simulation publishes an event rather than
    /// polling or allocating data every frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirePropagationSystem))]
    public sealed class FireVisualController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _fireParticleSystem;
        [SerializeField] private Light _fireLight;
        [SerializeField, Min(0f)] private float _maximumEmissionRate = 40f;
        [SerializeField, Min(0f)] private float _maximumLightIntensity = 3.5f;

        private FirePropagationSystem _firePropagationSystem;

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

        private void HandleAverageIntensityChanged(float averageIntensity)
        {
            if (_fireParticleSystem != null)
            {
                ParticleSystem.EmissionModule emissionModule = _fireParticleSystem.emission;
                emissionModule.rateOverTime = _maximumEmissionRate * averageIntensity;
            }

            if (_fireLight != null)
            {
                _fireLight.intensity = _maximumLightIntensity * averageIntensity;
            }
        }
    }
}
