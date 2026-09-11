using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace MRFireSafety.Core.Spatial
{
    /// <summary>
    /// Places one virtual training prop on the selected physical floor. It uses an anchor when the
    /// provider supports one and falls back to tracked world placement when anchors are unavailable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VirtualPropPlacementController : MonoBehaviour
    {
        [SerializeField] private SpatialAwarenessSystem _spatialAwarenessSystem;
        [SerializeField] private SpatialAnchorService _spatialAnchorService;
        [SerializeField] private Transform _trainingPropTransform;
        [SerializeField] private Transform _headTransform;
        [SerializeField, Min(0.5f)] private float _placementDistance = 1.5f;
        [SerializeField] private bool _placeAutomaticallyWhenFloorDetected = true;

        /// <summary>
        /// Raised after the training prop has been placed on a physical floor.
        /// </summary>
        public event Action<Pose> PropPlaced;

        /// <summary>
        /// Gets whether the training prop has already been placed in the current session.
        /// </summary>
        public bool IsPlaced { get; private set; }

        private void Awake()
        {
            if (_spatialAwarenessSystem == null)
            {
                _spatialAwarenessSystem = FindFirstObjectByType<SpatialAwarenessSystem>();
            }
        }

        private void OnEnable()
        {
            if (_spatialAwarenessSystem != null)
            {
                _spatialAwarenessSystem.FloorPlaneChanged += HandleFloorPlaneChanged;
            }
        }

        private void OnDisable()
        {
            if (_spatialAwarenessSystem != null)
            {
                _spatialAwarenessSystem.FloorPlaneChanged -= HandleFloorPlaneChanged;
            }
        }

        /// <summary>
        /// Places the training prop at a selected world pose on the active physical floor.
        /// </summary>
        /// <param name="worldPose">Desired placement pose in world coordinates.</param>
        /// <returns>True when placement succeeds; otherwise false.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required placement dependencies are missing.</exception>
        public bool TryPlaceAtPose(Pose worldPose)
        {
            if (_trainingPropTransform == null || _spatialAwarenessSystem == null)
            {
                throw new InvalidOperationException("A training prop and spatial-awareness system are required for placement.");
            }

            if (!_spatialAwarenessSystem.TryGetFloorPlane(out ARPlane floorPlane))
            {
                return false;
            }

            bool isAnchored = false;
            if (_spatialAnchorService != null)
            {
                try
                {
                    isAnchored = _spatialAnchorService.TryAnchorContent(floorPlane, worldPose, _trainingPropTransform);
                }
                catch (InvalidOperationException exception)
                {
                    Debug.LogWarning("MR Fire Safety: anchor provider is unavailable; using tracked world placement. " + exception.Message, this);
                }
            }
            if (!isAnchored)
            {
                _trainingPropTransform.SetPositionAndRotation(worldPose.position, worldPose.rotation);
            }

            IsPlaced = true;
            PropPlaced?.Invoke(worldPose);
            return true;
        }

        private void HandleFloorPlaneChanged(ARPlane floorPlane)
        {
            if (floorPlane == null || IsPlaced || !_placeAutomaticallyWhenFloorDetected)
            {
                return;
            }

            TryPlaceAtPose(GetAutomaticPlacementPose(floorPlane));
        }

        private Pose GetAutomaticPlacementPose(ARPlane floorPlane)
        {
            Transform referenceTransform = _headTransform == null ? transform : _headTransform;
            Vector3 forwardOnFloor = Vector3.ProjectOnPlane(referenceTransform.forward, Vector3.up).normalized;
            if (forwardOnFloor.sqrMagnitude < 0.001f)
            {
                forwardOnFloor = Vector3.forward;
            }

            Vector3 placementPosition = referenceTransform.position + (forwardOnFloor * _placementDistance);
            placementPosition.y = floorPlane.center.y;
            Quaternion placementRotation = Quaternion.LookRotation(-forwardOnFloor, Vector3.up);
            return new Pose(placementPosition, placementRotation);
        }
    }
}
