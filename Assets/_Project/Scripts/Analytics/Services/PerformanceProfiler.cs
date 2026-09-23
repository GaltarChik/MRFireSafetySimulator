using System;
using UnityEngine;

namespace MRFireSafety.Analytics.Services
{
    /// <summary>
    /// Samples frame rate at a fixed interval for lightweight in-app performance telemetry.
    /// This complements, rather than replaces, Unity Profiler measurements on the target headset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PerformanceProfiler : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _sampleInterval = 0.5f;
        [SerializeField, Min(1f)] private float _targetFramesPerSecond = 72f;

        private float _elapsedSampleTime;
        private int _framesSinceLastSample;
        private float _averageFramesPerSecond;
        private float _minimumFramesPerSecond = float.MaxValue;
        private int _sampleCount;

        /// <summary>
        /// Raised after a new frame-rate sample is calculated.
        /// </summary>
        public event Action<float> FrameRateSampled;

        /// <summary>
        /// Raised when a sampled frame rate falls below the configured target frame rate.
        /// </summary>
        public event Action<float> FrameRateBelowTarget;

        /// <summary>
        /// Gets the arithmetic mean of all collected frame-rate samples.
        /// </summary>
        public float AverageFramesPerSecond => _averageFramesPerSecond;

        /// <summary>
        /// Gets the lowest collected frame-rate sample, or zero before the first sample.
        /// </summary>
        public float MinimumFramesPerSecond => _sampleCount == 0 ? 0f : _minimumFramesPerSecond;

        /// <summary>
        /// Gets the frame rate below which a sample is reported as a performance shortfall.
        /// </summary>
        public float TargetFramesPerSecond => _targetFramesPerSecond;

        /// <summary>
        /// Sets the frame rate the session is expected to sustain. The training scene resolves this
        /// from the refresh rate reported by the XR display, which differs between headsets.
        /// </summary>
        /// <param name="targetFramesPerSecond">Expected sustained frame rate, in frames per second.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the target is not positive.</exception>
        public void SetTargetFrameRate(float targetFramesPerSecond)
        {
            if (targetFramesPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetFramesPerSecond), "The target frame rate must be positive.");
            }

            _targetFramesPerSecond = targetFramesPerSecond;
        }

        private void Update()
        {
            _elapsedSampleTime += Time.unscaledDeltaTime;
            _framesSinceLastSample++;
            if (_elapsedSampleTime < _sampleInterval)
            {
                return;
            }

            float framesPerSecond = _framesSinceLastSample / _elapsedSampleTime;
            _sampleCount++;
            _averageFramesPerSecond += (framesPerSecond - _averageFramesPerSecond) / _sampleCount;
            _minimumFramesPerSecond = Mathf.Min(_minimumFramesPerSecond, framesPerSecond);
            _elapsedSampleTime = 0f;
            _framesSinceLastSample = 0;
            FrameRateSampled?.Invoke(framesPerSecond);
            if (framesPerSecond < _targetFramesPerSecond)
            {
                FrameRateBelowTarget?.Invoke(framesPerSecond);
            }
        }

        /// <summary>
        /// Clears all sampled frame-rate statistics for a new training session.
        /// </summary>
        public void ResetSamples()
        {
            _elapsedSampleTime = 0f;
            _framesSinceLastSample = 0;
            _averageFramesPerSecond = 0f;
            _minimumFramesPerSecond = float.MaxValue;
            _sampleCount = 0;
        }
    }
}
