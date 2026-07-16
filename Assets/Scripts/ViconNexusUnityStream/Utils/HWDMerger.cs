using System;
using UnityEngine;
using UnityEngine.Events;

namespace ubco.ovilab.ViconUnityStream.Utils
{
    /// <summary>
    /// Repositions the MergerOffsetTransform transform such that the difference
    /// between the gloabl position and rotation difference between the
    /// xrHWD object and viconHWD object are below distanceThreshold
    /// and angleThreshold respectively.
    /// </summary>
    public class HWDMerger : MonoBehaviour
    {
        [Tooltip("The transform to which the offset is applied to. Ideally, this is a GameObject under the CameraOffset. The main camera transform in xrHWD should be in a child GameObject of this."), SerializeField]
        private Transform _mergerOffsetTransform;

        /// <summary>
        /// The XR Origin transform
        /// </summary>
        public Transform mergerOffsetTransform => _mergerOffsetTransform;

        [Tooltip("The Vicon HWD. Should be the transform that has the CustomHWDScript. Make sure to have the Vicon XR Devices disabled when using this component."), SerializeField]
        private Transform _viconHWD;

        /// <summary>
        /// The Vicon HWD. Should be the transform that has the CustomHWDScript. Make sure to have the Vicon XR Devices disabled when using this component.
        /// </summary>
        public Transform viconHWD => _viconHWD;

        [Tooltip("The transform with the camera component that follows the HWD position and rotation. Generally, this the Main Camera under the XR Origin."), SerializeField]
        private Transform _xrHWD;

        /// <summary>
        /// The transform with the camera component that follows the HWD position and rotation. Generally, this the Main Camera under the XR Origin.
        /// </summary>
        public Transform xrHWD => _xrHWD;

        [Tooltip("The distance threshold to achieve."), SerializeField]
        private float distanceThreshold = 0.001f;

        /// <summary>
        /// The distance threshold to achieve.
        /// </summary>
        public float DistanceThreshold => distanceThreshold;

        [Tooltip("The angle threshold to achieve"), SerializeField]
        private float angleThreshold = 0.5f;

        /// <summary>
        /// The angle threshold to achieve.
        /// </summary>
        public float AngleThreshold => angleThreshold;

        /// <summary>
        /// Automatically performs a merge after 5 seconds on awake
        /// </summary>
        [Tooltip("Automatically performs a merge after 5 seconds on awake"), SerializeField]
        private bool autoMerge;

        [Tooltip("Continuously compute the Vicon-to-Quest transform. When enabled, the offset is exposed via ViconToQuestTransform for hand scripts to apply, rather than being applied to the merger offset transform. The auto-merge still runs if enabled."), SerializeField]
        private bool continuousMode = false;

        /// <summary>
        /// Enable or disable continuous offset computation.
        /// </summary>
        public bool ContinuousMode { get => continuousMode; set => continuousMode = value; }

        [Tooltip("Low-pass filter strength for continuous mode. 0 = no filtering (instant), higher values = smoother but more lag."), SerializeField]
        [Range(0f, 1f)]
        private float continuousFilterBeta = 0.5f;

        /// <summary>
        /// Low-pass filter strength for continuous mode.
        /// </summary>
        public float ContinuousFilterBeta { get => continuousFilterBeta; set => continuousFilterBeta = value; }

        /// <summary>
        /// The current Vicon-to-Quest world-space transform. Apply this to a
        /// Vicon-world position/rotation to get the corresponding Quest-world values.
        /// Only updated when <see cref="continuousMode"/> is enabled.
        /// </summary>
        public Pose ViconToQuestTransform => new Pose(continuousFilteredPosition, continuousFilteredRotation);

        private Vector3 continuousFilteredPosition = Vector3.zero;
        private Quaternion continuousFilteredRotation = Quaternion.identity;
        private bool continuousFilterInitialized = false;

        [Tooltip("Offset from the Vicon HMD marker position to the user's eyes, in the headset's local space. For example, if markers are ~2cm above the eyes, set to (0, -0.02, 0). Applied when computing the Vicon-to-Quest transform in continuous mode."), SerializeField]
        private Vector3 eyeOffset = Vector3.zero;

        /// <summary>
        /// Offset from the Vicon HMD markers to the user's eyes, in headset-local space.
        /// </summary>
        public Vector3 EyeOffset { get => eyeOffset; set => eyeOffset = value; }

        [Tooltip("Called when successfully got the differences below the respective thresholds."), SerializeField] private UnityEvent onMergeSuccess;

        /// <summary>
        /// Called when successfully got the differences below the respective thresholds.
        /// </summary>
        public UnityEvent OnMergeSuccess => onMergeSuccess;

        [Tooltip("Called when failed to get the differences below the respective thresholds."), SerializeField] private UnityEvent onMergeFail;

        /// <summary>
        /// Called when failed to get the differences below the respective thresholds.
        /// </summary>
        public UnityEvent OnMergeFail => onMergeFail;

        [Tooltip("Called when the differences is above the respective thresholds."), SerializeField] private UnityEvent onDifferenceAboveThreshold;

        /// <summary>
        /// Called when the differences is above the respective thresholds.
        /// </summary>
        public UnityEvent OnDifferenceAboveThreshold => onDifferenceAboveThreshold;

        private void Awake()
        {
            if(autoMerge) Invoke(nameof(MergeHWDs), 5f);
        }

        /// <summary>
        /// Move the XR origin such that the Vicon HWD and XR HWD transforms are within specified thresholds.
        /// </summary>
        public void MergeHWDs()
        {
            bool success = false;
            for (int i = 0; i < 5; ++i)
            {
                mergerOffsetTransform.localPosition = Vector3.zero;
                mergerOffsetTransform.localRotation = Quaternion.identity;

                Transform parent = mergerOffsetTransform.parent;

                Quaternion localXRRotRelToParent = Quaternion.Inverse(parent.rotation) * xrHWD.rotation;
                Quaternion localViconRotRelToParent = Quaternion.Inverse(parent.rotation) * viconHWD.rotation;
                mergerOffsetTransform.localRotation = localViconRotRelToParent * Quaternion.Inverse(localXRRotRelToParent);

                Vector3 localXRPosRelToParent = parent.InverseTransformPoint(xrHWD.position);
                Vector3 localViconPosRelToParent = parent.InverseTransformPoint(viconHWD.position + viconHWD.rotation * eyeOffset);
                mergerOffsetTransform.localPosition = localViconPosRelToParent - localXRPosRelToParent;

                if (IsBelowThreshold())
                {
                    success = true;
                    OnMergeSuccess.Invoke();
                    break;
                }
            }
            if (!success)
            {
                OnMergeFail.Invoke();
                Debug.LogError($"Failed to merge vicon and xr");
            }
        }

        /// <summary>
        /// Returns true if differences between the viconHWD and xrHWD are below thresholds.
        /// </summary>
        public bool IsBelowThreshold()
        {
            return Vector3.Angle(viconHWD.forward, xrHWD.forward) < AngleThreshold && (viconHWD.position - xrHWD.position).magnitude < DistanceThreshold;
        }

        /// <inheritdoc />
        protected void OnEnable()
        {
            Debug.Assert(xrHWD.parent == mergerOffsetTransform, "mergerOffsetTransform's should be the parent of xrHWD");
        }

        /// <inheritdoc />
        protected void Update()
        {
            if (continuousMode)
            {
                ComputeContinuousOffset();
            }
            else if (!IsBelowThreshold())
            {
                OnDifferenceAboveThreshold.Invoke();
            }
        }

        /// <summary>
        /// Compute the Vicon-to-Quest world-space offset with low-pass filtering.
        /// </summary>
        private void ComputeContinuousOffset()
        {
            Quaternion rotOffset = xrHWD.rotation * Quaternion.Inverse(viconHWD.rotation);
            Vector3 viconEyePos = viconHWD.position + viconHWD.rotation * eyeOffset;
            Vector3 posOffset = xrHWD.position - rotOffset * viconEyePos;

            if (!continuousFilterInitialized)
            {
                continuousFilteredPosition = posOffset;
                continuousFilteredRotation = rotOffset;
                continuousFilterInitialized = true;
            }
            else
            {
                float alpha = 1f - continuousFilterBeta;
                continuousFilteredPosition = Vector3.Lerp(continuousFilteredPosition, posOffset, alpha);
                continuousFilteredRotation = Quaternion.Slerp(continuousFilteredRotation, rotOffset, alpha);
            }
        }
    }
}
