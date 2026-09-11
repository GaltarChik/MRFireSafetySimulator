using System;
using Meta.XR.MRUtilityKit;
using MRFireSafety.Core.Spatial;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace MRFireSafety.Core.Validation
{
    /// <summary>
    /// Reports actionable setup problems before a mixed-reality training session begins. The
    /// validator inspects whichever placement path the scene uses, the AR Foundation path or the
    /// Meta Scene API path, and treats Editor preview as a notice rather than a failure so that
    /// scene validation stays usable outside a device build.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MrReadinessValidator : MonoBehaviour
    {
        [SerializeField] private QuestFloorPlacementController _questFloorPlacementController;
        [SerializeField] private VirtualPropPlacementController _propPlacementController;
        [SerializeField] private SpatialAwarenessSystem _spatialAwarenessSystem;
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
                Debug.Log("MR Fire Safety: Editor preview is active. Passthrough, Scene API, and real floor tracking are validated only in a device build.", this);
            }

            if (_questFloorPlacementController != null)
            {
                return ValidateQuestScenePath();
            }

            if (_propPlacementController != null)
            {
                return ValidateArFoundationPath();
            }

            ReportIssue("No placement controller is present. Add either a VirtualPropPlacementController (AR Foundation) or a QuestFloorPlacementController (Meta Scene API).");
            return false;
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
            if (_questFloorPlacementController == null)
            {
                _questFloorPlacementController = FindFirstObjectByType<QuestFloorPlacementController>();
            }

            if (_propPlacementController == null)
            {
                _propPlacementController = FindFirstObjectByType<VirtualPropPlacementController>();
            }

            if (_spatialAwarenessSystem == null)
            {
                _spatialAwarenessSystem = FindFirstObjectByType<SpatialAwarenessSystem>();
            }
        }

        private bool ValidateQuestScenePath()
        {
            MRUK mruk = MRUK.Instance;
            if (mruk == null)
            {
                ReportIssue("Meta MR Utility Kit is missing. Add it to the scene before starting MR training.");
                return false;
            }

            if (!Application.isEditor && mruk.GetCurrentRoom() == null)
            {
                ReportIssue("No room model is available. Complete Quest Space Setup and grant Spatial Data permission.");
                return false;
            }

            return ReportSuccess("Meta Scene API");
        }

        private bool ValidateArFoundationPath()
        {
            if (FindFirstObjectByType<ARSession>() == null)
            {
                ReportIssue("No ARSession is present. Add an AR Session and an XR Origin rig to the training scene.");
                return false;
            }

            if (_spatialAwarenessSystem == null)
            {
                ReportIssue("SpatialAwarenessSystem is missing. Floor planes cannot be selected for prop placement.");
                return false;
            }

            if (!Application.isEditor && ARSession.state == ARSessionState.Unsupported)
            {
                ReportIssue("Mixed reality is unsupported on this device. Deploy the build to an OpenXR passthrough headset.");
                return false;
            }

            return ReportSuccess("AR Foundation");
        }

        private bool ReportSuccess(string pathName)
        {
            if (_logSuccessfulValidation)
            {
                Debug.Log($"MR Fire Safety: MR readiness validation passed for the {pathName} placement path.", this);
            }

            return true;
        }

        private void ReportIssue(string message)
        {
            Debug.LogWarning("MR Fire Safety: " + message, this);
            ReadinessIssueDetected?.Invoke(message);
        }
    }
}
