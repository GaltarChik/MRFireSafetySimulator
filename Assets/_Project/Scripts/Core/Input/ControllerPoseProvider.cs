using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MRFireSafety.Core.Input
{
    /// <summary>
    /// Applies OpenXR controller position and rotation actions to a virtual extinguisher transform.
    /// Untracked frames are rejected explicitly, because an untracked controller reports a zero
    /// quaternion that would otherwise be applied as a valid rotation. The provider has no desktop
    /// input fallback, so desktop simulation remains an Editor-only setup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerPoseProvider : MonoBehaviour
    {
        [SerializeField] private InputActionProperty _positionAction;
        [SerializeField] private InputActionProperty _rotationAction;
        [Tooltip("Optional tracking-state action. When assigned, poses are applied only while the controller reports that it is tracked.")]
        [SerializeField] private InputActionProperty _isTrackedAction;
        [SerializeField] private Transform _extinguisherTransform;
        [SerializeField] private Transform _trackingOriginTransform;

        private const float MinimumValidQuaternionMagnitude = 0.5f;

        private bool _isTracked;

        /// <summary>
        /// Raised whenever a tracked controller pose is applied to the extinguisher transform.
        /// </summary>
        public event Action<Pose> PoseApplied;

        /// <summary>
        /// Raised when the controller starts or stops reporting a usable tracked pose.
        /// </summary>
        public event Action<bool> TrackingChanged;

        /// <summary>
        /// Gets whether both required controller pose actions are assigned.
        /// </summary>
        public bool HasPoseActions => _positionAction.action != null && _rotationAction.action != null;

        /// <summary>
        /// Gets whether the controller currently reports a usable tracked pose.
        /// </summary>
        public bool IsTracked => _isTracked;

        private void OnEnable()
        {
            _positionAction.action?.Enable();
            _rotationAction.action?.Enable();
            _isTrackedAction.action?.Enable();
        }

        private void OnDisable()
        {
            _positionAction.action?.Disable();
            _rotationAction.action?.Disable();
            _isTrackedAction.action?.Disable();
            SetTracked(false);
        }

        private void Update()
        {
            if (!HasPoseActions || _extinguisherTransform == null)
            {
                return;
            }

            if (_isTrackedAction.action != null && !_isTrackedAction.action.IsPressed())
            {
                SetTracked(false);
                return;
            }

            Vector3 controllerLocalPosition = _positionAction.action.ReadValue<Vector3>();
            Quaternion controllerLocalRotation = _rotationAction.action.ReadValue<Quaternion>();

            // Quaternion equality is dot-product based and never matches the zero quaternion that an
            // untracked device reports, so the magnitude is checked directly.
            float rotationMagnitude = (controllerLocalRotation.x * controllerLocalRotation.x)
                + (controllerLocalRotation.y * controllerLocalRotation.y)
                + (controllerLocalRotation.z * controllerLocalRotation.z)
                + (controllerLocalRotation.w * controllerLocalRotation.w);
            if (rotationMagnitude < MinimumValidQuaternionMagnitude)
            {
                SetTracked(false);
                return;
            }

            Vector3 controllerPosition = _trackingOriginTransform == null
                ? controllerLocalPosition
                : _trackingOriginTransform.TransformPoint(controllerLocalPosition);
            Quaternion controllerRotation = _trackingOriginTransform == null
                ? controllerLocalRotation
                : _trackingOriginTransform.rotation * controllerLocalRotation;

            SetTracked(true);
            _extinguisherTransform.SetPositionAndRotation(controllerPosition, controllerRotation);
            PoseApplied?.Invoke(new Pose(controllerPosition, controllerRotation));
        }

        private void SetTracked(bool isTracked)
        {
            if (_isTracked == isTracked)
            {
                return;
            }

            _isTracked = isTracked;
            TrackingChanged?.Invoke(isTracked);
        }
    }
}
