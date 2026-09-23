using UnityEngine;

namespace MRFireSafety.UI.Controllers
{
    /// <summary>
    /// Keeps an interface panel comfortably readable in a head-mounted display without rigidly
    /// locking it to the head. A panel parented to the camera moves with every small head motion,
    /// which reads as if the world is dragging and is a common source of discomfort. This controller
    /// instead leaves the panel still while the trainee looks around inside a dead zone, and eases
    /// it back into view only once their gaze has moved past that threshold.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeadFollowPanelController : MonoBehaviour
    {
        [SerializeField] private Transform _headTransform;
        [Tooltip("Distance in front of the trainee at which the panel settles, in metres.")]
        [SerializeField, Min(0.3f)] private float _followDistance = 1.6f;
        [Tooltip("Vertical offset from the eye line, in metres. Negative keeps the panel below the centre of view.")]
        [SerializeField] private float _verticalOffset = -0.25f;
        [Tooltip("Angle the gaze may move away from the panel before it starts following, in degrees.")]
        [SerializeField, Range(0f, 45f)] private float _deadZoneAngle = 14f;
        [Tooltip("How quickly the panel eases toward its resting place once it starts following.")]
        [SerializeField, Min(0.1f)] private float _followSpeed = 2.5f;
        [Tooltip("Keep the panel upright instead of copying head roll, which is far more comfortable to read.")]
        [SerializeField] private bool _keepsPanelUpright = true;

        private bool _isFollowing;

        /// <summary>
        /// Gets whether the panel is currently easing back toward the centre of view.
        /// </summary>
        public bool IsFollowing => _isFollowing;

        /// <summary>
        /// Assigns the head transform the panel follows, without relying on editor-only serialized
        /// property access.
        /// </summary>
        /// <param name="headTransform">Transform of the XR camera.</param>
        public void Configure(Transform headTransform)
        {
            _headTransform = headTransform;
        }

        private void Awake()
        {
            if (_headTransform == null && Camera.main != null)
            {
                _headTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            if (_headTransform == null)
            {
                return;
            }

            // Start settled, so the panel does not fly in from the origin on the first frame.
            transform.SetPositionAndRotation(GetRestingPosition(), GetRestingRotation());
        }

        private void LateUpdate()
        {
            if (_headTransform == null)
            {
                return;
            }

            Vector3 restingPosition = GetRestingPosition();
            UpdateFollowState(restingPosition);
            if (!_isFollowing)
            {
                return;
            }

            float easing = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, restingPosition, easing);
            transform.rotation = Quaternion.Slerp(transform.rotation, GetRestingRotation(), easing);

            if (Vector3.Distance(transform.position, restingPosition) < 0.01f)
            {
                _isFollowing = false;
            }
        }

        private void UpdateFollowState(Vector3 restingPosition)
        {
            if (_isFollowing)
            {
                return;
            }

            Vector3 toPanel = transform.position - _headTransform.position;
            Vector3 toResting = restingPosition - _headTransform.position;
            if (toPanel.sqrMagnitude < 0.0001f || toResting.sqrMagnitude < 0.0001f)
            {
                _isFollowing = true;
                return;
            }

            _isFollowing = Vector3.Angle(toPanel, toResting) > _deadZoneAngle;
        }

        private Vector3 GetRestingPosition()
        {
            return _headTransform.position
                + (_headTransform.forward * _followDistance)
                + (Vector3.up * _verticalOffset);
        }

        private Quaternion GetRestingRotation()
        {
            if (!_keepsPanelUpright)
            {
                return Quaternion.LookRotation(transform.position - _headTransform.position, _headTransform.up);
            }

            Vector3 levelledForward = Vector3.ProjectOnPlane(_headTransform.forward, Vector3.up);
            if (levelledForward.sqrMagnitude < 0.0001f)
            {
                levelledForward = Vector3.forward;
            }

            return Quaternion.LookRotation(levelledForward, Vector3.up);
        }
    }
}
