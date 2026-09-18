using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.XR.CoreUtils;

namespace ubco.ovilab.ViconUnityStream
{
    public class CustomHWDScript : CustomSubjectScript
    {
        [Header("HWD Settings")]
        [Tooltip("If set or game object has XROrigin will configure the XROrigin.")]
        [SerializeField] private XROrigin xrOrigin;
        [Tooltip("Position offset to get \"True\" centre of the HMD based on vicon tracker markers. (in mm or 1/1000th of a Unity unit distance)")]
        [SerializeField] private Vector3 hmdPositionOffset;
        [Tooltip("Rotation offset to get  \"True\" centre of the HMD based on vicon tracker markers.")]
        [SerializeField] private Quaternion hmdRotationOffset = Quaternion.identity;

        [Header("SWD One Euro filter settings")]
        [Tooltip("Enables filter for position")]
        [SerializeField] private bool applyPosFilter = false;
        [Tooltip("Filter min cutoff for position filter")]
        [SerializeField] private float posFilterMinCutoff = 0.1f;
        [Tooltip("Beta value for position filter")]
        [SerializeField] private float posFilterBeta = 50;

        [Space()]
        [Tooltip("Enables filter for rotation")]
        [SerializeField] private bool applyRotFilter = false;
        [Tooltip("Filter min cutoff for rotation filter")]
        [SerializeField] private float rotFilterMinCutoff = 0.1f;
        [Tooltip("Beta value for rotation filter")]
        [SerializeField] private float rotFilterBeta = 50;

        /// <summary>
        /// Read access to the serialized HWD offsets, for calibration capture
        /// provenance: logging the offsets actually applied per sample makes any
        /// stale or changed offset visible in the data instead of the console.
        /// </summary>
        public Vector3 HmdPositionOffset => hmdPositionOffset;
        public Quaternion HmdRotationOffset => hmdRotationOffset;
        public bool ApplyPosFilter => applyPosFilter;
        public bool ApplyRotFilter => applyRotFilter;

        private OneEuroFilter<Quaternion> rotFilter;
        private OneEuroFilter<Vector3> posFilter;
        private Vector3 imaginaryCentre;
        protected override void Start()
        {
            base.Start();

            if (xrOrigin == null)
            {
                xrOrigin = GetComponent<XROrigin>();
            }

            if (xrOrigin != null)
            {
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            }
        }

        /// <inheritdoc />
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                rotFilter = new OneEuroFilter<Quaternion>(90, rotFilterMinCutoff, rotFilterBeta);
                posFilter = new OneEuroFilter<Vector3>(90, posFilterMinCutoff, posFilterBeta);
            }
        }

        /// <inheritdoc />
        protected override Dictionary<string, Vector3> ProcessSegments(Dictionary<string, Vector3> segments, ViconStreamData viconStreamData)
        {
            Vector3 forward = segments["base2"] - segments["base1"];
            Vector3 right = segments["base3"] - segments["base4"];
            Vector3 up = Vector3.Cross(forward, right);
            if (forward == Vector3.zero || up == Vector3.zero) return segments;
            Quaternion rotation = Quaternion.LookRotation(forward, up);

            Vector3 base1Pos = segments["base1"];

            if ((applyPosFilter && posFilter == null) || (applyRotFilter && rotFilter == null))
            {
                OnValidate();
            }

            if (applyPosFilter)
            {
                base1Pos = posFilter.Filter(base1Pos, Time.realtimeSinceStartup);
            }

            if (applyRotFilter)
            {
                rotation = rotFilter.Filter(rotation, Time.realtimeSinceStartup);
            }

            rotation = rotation * hmdRotationOffset;

            base1Pos += (forward.normalized * hmdPositionOffset.z + up.normalized * hmdPositionOffset.y + right.normalized * hmdPositionOffset.x);

            ViconXRLoader.TrySetXRDeviceData(base1Pos * viconUnitsToUnityUnits, rotation);

            foreach (var key in segmentsRotation.Keys.ToArray())
            {
                segmentsRotation[key] = rotation;
            }

            segments["base1"] = base1Pos;
            return segments;
        }
    }
}
