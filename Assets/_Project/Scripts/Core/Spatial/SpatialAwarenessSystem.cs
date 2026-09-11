using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MRFireSafety.Core.Spatial
{
    /// <summary>
    /// Selects the largest tracked horizontal-up AR plane as the current physical floor. The system
    /// contains no rendering logic and exposes floor changes through an event for placement clients.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARPlaneManager))]
    public sealed class SpatialAwarenessSystem : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager _planeManager;
        [SerializeField, Min(0.1f)] private float _minimumFloorAreaSquareMeters = 1f;

        private ARPlane _currentFloorPlane;

        /// <summary>
        /// Raised when a suitable physical floor plane becomes available or is replaced.
        /// </summary>
        public event Action<ARPlane> FloorPlaneChanged;

        /// <summary>
        /// Gets the selected physical floor plane, or null until tracking supplies one.
        /// </summary>
        public ARPlane CurrentFloorPlane => _currentFloorPlane;

        /// <summary>
        /// Gets whether a tracked physical floor plane is currently available.
        /// </summary>
        public bool HasFloorPlane => _currentFloorPlane != null;

        private void Awake()
        {
            if (_planeManager == null)
            {
                _planeManager = GetComponent<ARPlaneManager>();
            }
        }

        private void OnEnable()
        {
            _planeManager.trackablesChanged.AddListener(HandleTrackablesChanged);
        }

        private void OnDisable()
        {
            if (_planeManager == null)
            {
                return;
            }

            _planeManager.trackablesChanged.RemoveListener(HandleTrackablesChanged);
        }

        private void Start()
        {
            UpdateCurrentFloorPlane();
        }

        /// <summary>
        /// Attempts to retrieve the selected tracked floor plane.
        /// </summary>
        /// <param name="floorPlane">Receives the current floor plane when the method succeeds.</param>
        /// <returns>True when a floor plane is available; otherwise false.</returns>
        public bool TryGetFloorPlane(out ARPlane floorPlane)
        {
            floorPlane = _currentFloorPlane;
            return floorPlane != null;
        }

        private void HandleTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> eventArgs)
        {
            UpdateCurrentFloorPlane();
        }

        private void UpdateCurrentFloorPlane()
        {
            ARPlane bestFloorPlane = FindBestFloorPlane();
            if (bestFloorPlane == _currentFloorPlane)
            {
                return;
            }

            _currentFloorPlane = bestFloorPlane;
            FloorPlaneChanged?.Invoke(_currentFloorPlane);
        }

        private ARPlane FindBestFloorPlane()
        {
            ARPlane bestFloorPlane = null;
            float bestArea = 0f;

            foreach (ARPlane candidatePlane in _planeManager.trackables)
            {
                if (candidatePlane.trackingState != TrackingState.Tracking || candidatePlane.alignment != PlaneAlignment.HorizontalUp)
                {
                    continue;
                }

                float candidateArea = candidatePlane.size.x * candidatePlane.size.y;
                if (candidateArea < _minimumFloorAreaSquareMeters || candidateArea <= bestArea)
                {
                    continue;
                }

                bestArea = candidateArea;
                bestFloorPlane = candidatePlane;
            }

            return bestFloorPlane;
        }
    }
}
