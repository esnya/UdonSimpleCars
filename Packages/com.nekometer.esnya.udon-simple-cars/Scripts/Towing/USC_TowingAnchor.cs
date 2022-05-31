
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class USC_TowingAnchor : UdonSharpBehaviour
    {
        #region Public Variables
        [Tooltip("Default: VRCObjectSync in parent or find from SaccFlightAndVehicles")]
        public GameObject ownerDetector;
        public string keyword = "DEFAULT";
        #endregion

        #region NonSerialized Variables
        [NonSerialized] public Rigidbody attachedRigidbody;
        [NonSerialized] public WheelCollider attachedWheelCollider;
        [NonSerialized] public string[] keywords;
        #endregion

        #region Unity Events
        private void Start()
        {
            attachedRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            if (!ownerDetector) ownerDetector = attachedRigidbody.gameObject;

            attachedWheelCollider = GetComponentInParent<WheelCollider>();

            keywords = keyword.Split(',');
        }
        #endregion
    }
}
