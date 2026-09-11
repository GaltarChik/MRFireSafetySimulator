using System;
using MRFireSafety.Core.Spatial;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace MRFireSafety.Core.Validation
{
    /// <summary>
    /// Reports actionable setup problems before a mixed-reality training session begins. The
    /// validator checks the AR Foundation rig that the prototype depends on and treats Editor
    /// preview as a notice rather than a failure, so scene validation stays usable outside a
    /// device build.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MrReadinessValidator : MonoBehaviour
    {
        [SerializeField] private ARSession _arSession;
        [SerializeField] private ARPlaneManager _planeManager;
        [SerializeField] private ARAnchorManager _anchorManager;
        [SerializeField] private SpatialAwarenessSystem _spatialAwarenessSystem;
        [SerializeField] private VirtualPropPlacementController _propPlacementController;
        [SerializeField] private bool _validatesOnStart = true;
        [SerializeField] private bool _logSuccessfulValidation = true;

        /// <summary>
        /// Raised when a runtime setup issue needs attention before MR placement can begin.
        /// </summary>
        public event Action<string> ReadinessIssueDetected;

        /// <summary>
        /// Validates the MR scene setup and emits clear diagnostic messages.
        /// </summary>
        /// <returns>True when the configured runtime is ready; otherwise false.</returns>
        public bool ValidateReadiness()
        {
            ResolveComponents();

            if (Application.isEditor)
            {
                Debug.Log("MR Fire Safety: Editor preview is active. Passthrough and real floor tracking are validated only in a device build.", this);
            }

            if (_arSession == null)
            {
                ReportIssue("No ARSession is present. Add an AR Session and an XR Origin rig to the training scene.");
                return false;
            }

            if (_planeManager == null)
            {
                ReportIssue("No ARPlaneManager is present on the XR Origin. The physical floor cannot be detected.");
                return false;
            }

            if (_anchorManager == null)
            {
                ReportIssue("No ARAnchorManager is present on the XR Origin. The training prop cannot be world-locked.");
                return false;
            }

            if (_spatialAwarenessSystem == null)
            {
                ReportIssue("SpatialAwarenessSystem is missing. Floor planes cannot be selected for prop placement.");
                return false;
            }

            if (_propPlacementController == null)
            {
                ReportIssue("VirtualPropPlacementController is missing. The training prop cannot be placed on the physical floor.");
                return false;
            }

            if (!Application.isEditor && ARSession.state == ARSessionState.Unsupported)
            {
                ReportIssue("Mixed reality is unsupported on this device. Deploy the build to an OpenXR passthrough headset.");
                return false;
            }

            if (_logSuccessfulValidation)
            {
                Debug.Log("MR Fire Safety: AR Foundation readiness validation passed.", this);
            }

            return true;
        }

        private void Start()
        {
            if (_validatesOnStart)
            {
                ValidateReadiness();
            }
        }

        private void ResolveComponents()
        {
            if (_arSession == null)
            {
                _arSession = FindFirstObjectByType<ARSession>();
            }

            if (_planeManager == null)
            {
                _planeManager = FindFirstObjectByType<ARPlaneManager>();
            }

            if (_anchorManager == null)
            {
                _anchorManager = FindFirstObjectByType<ARAnchorManager>();
            }

            if (_spatialAwarenessSystem == null)
            {
                _spatialAwarenessSystem = FindFirstObjectByType<SpatialAwarenessSystem>();
            }

            if (_propPlacementController == null)
            {
                _propPlacementController = FindFirstObjectByType<VirtualPropPlacementController>();
            }
        }

        private void ReportIssue(string message)
        {
            Debug.LogWarning("MR Fire Safety: " + message, this);
            ReadinessIssueDetected?.Invoke(message);
        }
    }
}
