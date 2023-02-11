
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
        public WheelCollider[] steeringWheels = { };
        #endregion

        #region NonSerialized Variables
        [NonSerialized] public Rigidbody attachedRigidbody;
        [NonSerialized] public string[] keywords;
        #endregion

        #region Unity Events
        private void Start()
        {
            attachedRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            if (!ownerDetector) ownerDetector = attachedRigidbody.gameObject;

            if (steeringWheels == null || steeringWheels.Length == 0)
            {
                var wheelInParent = GetComponentInParent<WheelCollider>();
                steeringWheels = wheelInParent ? new[] { wheelInParent } : new WheelCollider[0];
            }

            keywords = keyword.Split(',');
        }
        #endregion
    }
}
