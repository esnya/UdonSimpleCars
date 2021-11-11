
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class USC_TowingAnchor : UdonSharpBehaviour
    {
        #region Public Variables
        [Tooltip("Default: VRCObjectSync in parent")]
        public GameObject ownerDetector;
        #endregion

        #region NonSerialized Variables
        [NonSerialized] public Rigidbody vehicleRigidbody;
        [NonSerialized] public WheelCollider attachedWheelCollider;
        #endregion

        #region Unity Events
        private void Start()
        {
            var objectSync = (VRCObjectSync)GetComponentInParent(typeof(VRCObjectSync));
            if (ownerDetector == null && objectSync != null) ownerDetector = objectSync.gameObject;

            if (objectSync != null) vehicleRigidbody = objectSync.GetComponent<Rigidbody>();
            if (vehicleRigidbody == null) vehicleRigidbody = transform.parent.GetComponentInParent<Rigidbody>();

            attachedWheelCollider = GetComponentInParent<WheelCollider>();
        }
        #endregion
    }
}
