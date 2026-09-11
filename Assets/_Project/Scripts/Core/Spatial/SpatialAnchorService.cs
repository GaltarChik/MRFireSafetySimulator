using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace MRFireSafety.Core.Spatial
{
    /// <summary>
    /// Creates an AR Foundation anchor attached to a selected floor plane and parents virtual
    /// training content to it. If the platform has no anchor subsystem, placement fails safely.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARAnchorManager))]
    public sealed class SpatialAnchorService : MonoBehaviour
    {
        [SerializeField] private ARAnchorManager _anchorManager;

        private ARAnchor _activeAnchor;

        /// <summary>
        /// Raised when content is successfully attached to a new spatial anchor.
        /// </summary>
        public event Action<ARAnchor> AnchorCreated;

        /// <summary>
        /// Gets the active placement anchor, or null when no anchor is active.
        /// </summary>
        public ARAnchor ActiveAnchor => _activeAnchor;

        private void Awake()
        {
            if (_anchorManager == null)
            {
                _anchorManager = GetComponent<ARAnchorManager>();
            }
        }

        /// <summary>
        /// Anchors a virtual object to a pose on a tracked floor plane.
        /// </summary>
        /// <param name="floorPlane">Tracked plane that should own the anchor.</param>
        /// <param name="worldPose">Desired world-space pose of the virtual object.</param>
        /// <param name="contentTransform">Virtual content to parent beneath the anchor.</param>
        /// <returns>True when the anchor is created; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when floorPlane or contentTransform is null.</exception>
        public bool TryAnchorContent(ARPlane floorPlane, Pose worldPose, Transform contentTransform)
        {
            if (floorPlane == null)
            {
                throw new ArgumentNullException(nameof(floorPlane));
            }

            if (contentTransform == null)
            {
                throw new ArgumentNullException(nameof(contentTransform));
            }

            RemoveActiveAnchor();
            ARAnchor anchor = _anchorManager.AttachAnchor(floorPlane, worldPose);
            if (anchor == null)
            {
                return false;
            }

            contentTransform.SetParent(anchor.transform, true);
            contentTransform.SetPositionAndRotation(worldPose.position, worldPose.rotation);
            _activeAnchor = anchor;
            AnchorCreated?.Invoke(_activeAnchor);
            return true;
        }

        /// <summary>
        /// Removes the active anchor and keeps its child content in world space.
        /// </summary>
        public void RemoveActiveAnchor()
        {
            if (_activeAnchor == null)
            {
                return;
            }

            foreach (Transform childTransform in _activeAnchor.transform)
            {
                childTransform.SetParent(null, true);
            }

            _anchorManager.TryRemoveAnchor(_activeAnchor);
            _activeAnchor = null;
        }
    }
}
