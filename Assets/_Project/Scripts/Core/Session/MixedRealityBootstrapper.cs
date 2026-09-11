using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace MRFireSafety.Core.Session
{
    /// <summary>
    /// Brings up the mixed-reality session for the training prototype: it waits for the OpenXR
    /// AR session to reach a usable state, configures the passthrough-compatible camera clear
    /// settings, and publishes readiness through events. All work is event or coroutine driven,
    /// so no per-frame polling is added to the mobile XR frame budget.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MixedRealityBootstrapper : MonoBehaviour
    {
        [Header("AR Foundation")]
        [SerializeField] private ARSession _arSession;
        [SerializeField] private ARCameraManager _cameraManager;
        [SerializeField] private ARCameraBackground _cameraBackground;

        [Header("Startup")]
        [SerializeField, Min(1f)] private float _sessionStartTimeoutSeconds = 10f;
        [SerializeField, Min(1f)] private float _permissionTimeoutSeconds = 30f;
        [SerializeField] private bool _logSessionState = true;

        /// <summary>
        /// Android runtime permission that gates access to the room model on Meta headsets. Plane
        /// detection returns nothing until the trainee grants it, even though the manifest declares it.
        /// </summary>
        private const string SpatialDataPermission = "com.oculus.permission.USE_SCENE";

        private Camera _passthroughCamera;
        private bool _isPassthroughReady;

        /// <summary>
        /// Raised once the AR session is tracking and the passthrough camera is configured.
        /// </summary>
        public event Action PassthroughReady;

        /// <summary>
        /// Raised when the session cannot start, carrying a diagnostic message for the user interface.
        /// </summary>
        public event Action<string> SessionUnavailable;

        /// <summary>
        /// Gets whether the passthrough camera has been configured and the session is usable.
        /// </summary>
        public bool IsPassthroughReady => _isPassthroughReady;

        /// <summary>
        /// Gets the current AR session state reported by the active XR provider.
        /// </summary>
        public ARSessionState CurrentSessionState => ARSession.state;

        private void Awake()
        {
            if (_arSession == null)
            {
                _arSession = FindFirstObjectByType<ARSession>();
            }

            if (_cameraManager == null)
            {
                _cameraManager = FindFirstObjectByType<ARCameraManager>();
            }

            if (_cameraBackground == null && _cameraManager != null)
            {
                _cameraBackground = _cameraManager.GetComponent<ARCameraBackground>();
            }

            _passthroughCamera = _cameraManager == null ? null : _cameraManager.GetComponent<Camera>();
        }

        private void OnEnable()
        {
            ARSession.stateChanged += HandleSessionStateChanged;
        }

        private void OnDisable()
        {
            ARSession.stateChanged -= HandleSessionStateChanged;
        }

        private IEnumerator Start()
        {
            if (_arSession == null || _cameraManager == null)
            {
                ReportUnavailable("AR Session or AR Camera Manager is missing from the XR Origin rig.");
                yield break;
            }

            ConfigurePassthroughCamera();
            yield return RequestSpatialDataPermission();

            float elapsedTime = 0f;
            while (ARSession.state < ARSessionState.SessionInitializing && elapsedTime < _sessionStartTimeoutSeconds)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            if (ARSession.state < ARSessionState.SessionInitializing)
            {
                ReportUnavailable($"AR session did not start within {_sessionStartTimeoutSeconds:0} s (state: {ARSession.state}). Verify the OpenXR loader and the Meta passthrough feature.");
            }
        }

        /// <summary>
        /// Applies the camera settings required for an additive passthrough composition: the camera
        /// clears to fully transparent black so the device compositor shows the physical room behind
        /// the virtual training prop.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no camera is attached to the AR Camera Manager.</exception>
        public void ConfigurePassthroughCamera()
        {
            if (_passthroughCamera == null)
            {
                throw new InvalidOperationException("The AR Camera Manager requires a Camera component for passthrough composition.");
            }

            _passthroughCamera.clearFlags = CameraClearFlags.SolidColor;
            _passthroughCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            if (_cameraBackground != null)
            {
                _cameraBackground.enabled = true;
            }
        }

        /// <summary>
        /// Requests the Android spatial-data permission required to read the room model, and waits
        /// for the trainee to answer the system dialog. On other platforms the request is a no-op.
        /// </summary>
        /// <returns>An enumerator that completes once the permission is granted or the wait times out.</returns>
        private IEnumerator RequestSpatialDataPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(SpatialDataPermission))
            {
                yield break;
            }

            Permission.RequestUserPermission(SpatialDataPermission);

            float elapsedTime = 0f;
            while (!Permission.HasUserAuthorizedPermission(SpatialDataPermission) && elapsedTime < _permissionTimeoutSeconds)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!Permission.HasUserAuthorizedPermission(SpatialDataPermission))
            {
                ReportUnavailable("Spatial data permission was not granted, so the physical floor cannot be detected. Grant it in the system settings and restart the application.");
            }
#else
            yield break;
#endif
        }

        private void HandleSessionStateChanged(ARSessionStateChangedEventArgs eventArgs)
        {
            if (_logSessionState)
            {
                Debug.Log("MR Fire Safety: AR session state changed to " + eventArgs.state, this);
            }

            switch (eventArgs.state)
            {
                case ARSessionState.Unsupported:
                    ReportUnavailable("Mixed reality is unsupported on this device. Deploy the build to an OpenXR passthrough headset.");
                    break;
                case ARSessionState.SessionTracking when !_isPassthroughReady:
                    _isPassthroughReady = true;
                    PassthroughReady?.Invoke();
                    break;
            }
        }

        private void ReportUnavailable(string message)
        {
            Debug.LogWarning("MR Fire Safety: " + message, this);
            SessionUnavailable?.Invoke(message);
        }
    }
}
