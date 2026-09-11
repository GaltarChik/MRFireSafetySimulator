using System;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MRFireSafety.Core.Spatial
{
    /// <summary>
    /// Quest-specific placement adapter that obtains the user's physical floor from Meta MR Utility
    /// Kit and parents a virtual prop to its world-locked scene anchor after Scene API is ready.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestFloorPlacementController : MonoBehaviour
    {
        [SerializeField] private MRUK _mruk;
        [SerializeField] private Transform _trainingPropTransform;
        [SerializeField] private Transform _headTransform;
        [SerializeField, Min(0.5f)] private float _placementDistance = 1.5f;

        /// <summary>
        /// Raised when Meta Scene API has placed and world-locked the virtual training prop.
        /// </summary>
        public event Action<Pose> PropPlacedOnFloor;

        /// <summary>
        /// Gets whether the training prop has been placed on a Quest floor anchor.
        /// </summary>
        public bool IsPlaced { get; private set; }

        private void Awake()
        {
            if (_mruk == null)
            {
                _mruk = MRUK.Instance;
            }
        }

        private void Start()
        {
            if (_mruk == null)
            {
                Debug.LogWarning("MR Fire Safety: MRUK is missing. Add the Meta MR Utility Kit building block before Quest floor placement.", this);
                return;
            }

            _mruk.RegisterSceneLoadedCallback(PlaceOnCurrentRoomFloor);
        }

        /// <summary>
        /// Places the configured prop on the first floor anchor in the currently tracked Quest room.
        /// </summary>
        /// <returns>True when placement succeeds; otherwise false.</returns>
        public bool TryPlaceOnCurrentRoomFloor()
        {
            if (_mruk == null || _trainingPropTransform == null)
            {
                return false;
            }

            MRUKRoom currentRoom = _mruk.GetCurrentRoom();
            if (currentRoom == null || currentRoom.FloorAnchors.Count == 0)
            {
                return false;
            }

            MRUKAnchor floorAnchor = currentRoom.FloorAnchors[0];
            Transform referenceTransform = _headTransform == null ? transform : _headTransform;
            Vector3 forwardOnFloor = Vector3.ProjectOnPlane(referenceTransform.forward, Vector3.up).normalized;
            if (forwardOnFloor.sqrMagnitude < 0.001f)
            {
                forwardOnFloor = Vector3.forward;
            }

            Vector3 placementPosition = referenceTransform.position + (forwardOnFloor * _placementDistance);
            placementPosition.y = floorAnchor.transform.position.y;
            Quaternion placementRotation = Quaternion.LookRotation(-forwardOnFloor, Vector3.up);
            _trainingPropTransform.SetPositionAndRotation(placementPosition, placementRotation);
            _trainingPropTransform.SetParent(floorAnchor.transform, true);

            IsPlaced = true;
            PropPlacedOnFloor?.Invoke(new Pose(placementPosition, placementRotation));
            return true;
        }

        private void PlaceOnCurrentRoomFloor()
        {
            if (!IsPlaced)
            {
                TryPlaceOnCurrentRoomFloor();
            }
        }
    }
}
