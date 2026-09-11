using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MRFireSafety.Analytics.Services
{
    /// <summary>
    /// Resolves the frame-pacing target for the training prototype. On an XR device the compositor
    /// owns frame pacing, so the service reads the display refresh rate reported by the running
    /// display subsystem instead of overriding it: writing <see cref="Application.targetFrameRate"/>
    /// there is ignored at best and caps a 90 Hz or 120 Hz headset at worst. The frame-rate cap is
    /// applied only when no XR display is present, which covers Editor and desktop preview.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PerformanceConfigurationService : MonoBehaviour
    {
        [Tooltip("Minimum frame rate the prototype must sustain. Used as the evaluation target when no XR display reports a refresh rate.")]
        [SerializeField, Min(30f)] private float _minimumTargetFrameRate = 72f;
        [SerializeField] private bool _logResolvedTarget = true;

        private readonly List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>(1);
        private float _resolvedTargetFrameRate;

        /// <summary>
        /// Raised after the frame-rate target has been resolved for the current device.
        /// </summary>
        public event Action<float> TargetFrameRateResolved;

        /// <summary>
        /// Gets the frame rate the session is expected to sustain, in frames per second.
        /// </summary>
        public float ResolvedTargetFrameRate => _resolvedTargetFrameRate;

        /// <summary>
        /// Gets whether a running XR display subsystem supplied the frame-rate target.
        /// </summary>
        public bool IsDrivenByXrDisplay { get; private set; }

        private void Start()
        {
            _resolvedTargetFrameRate = ResolveTargetFrameRate();
            if (!IsDrivenByXrDisplay)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Mathf.RoundToInt(_resolvedTargetFrameRate);
            }

            if (_logResolvedTarget)
            {
                string source = IsDrivenByXrDisplay ? "XR display refresh rate" : "application frame-rate cap";
                Debug.Log($"MR Fire Safety: performance target set to {_resolvedTargetFrameRate:0} FPS via {source}.", this);
            }

            TargetFrameRateResolved?.Invoke(_resolvedTargetFrameRate);
        }

        /// <summary>
        /// Determines the frame-rate target for the current device.
        /// </summary>
        /// <returns>The refresh rate reported by the XR display, or the configured minimum target.</returns>
        public float ResolveTargetFrameRate()
        {
            IsDrivenByXrDisplay = false;
            SubsystemManager.GetSubsystems(_displaySubsystems);

            for (int subsystemIndex = 0; subsystemIndex < _displaySubsystems.Count; subsystemIndex++)
            {
                XRDisplaySubsystem displaySubsystem = _displaySubsystems[subsystemIndex];
                if (displaySubsystem == null || !displaySubsystem.running)
                {
                    continue;
                }

                if (displaySubsystem.TryGetDisplayRefreshRate(out float refreshRate) && refreshRate > 0f)
                {
                    IsDrivenByXrDisplay = true;
                    return refreshRate;
                }
            }

            return _minimumTargetFrameRate;
        }
    }
}
