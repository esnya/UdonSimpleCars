
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
        #endregion

        #region NonSerialized Variables
        [NonSerialized] public Rigidbody vehicleRigidbody;
        [NonSerialized] public WheelCollider attachedWheelCollider;
        #endregion

        #region Unity Events
        private void Start()
        {
            var objectSync = (VRCObjectSync)GetComponentInParent(typeof(VRCObjectSync));
            if (objectSync)
            {
                vehicleRigidbody = objectSync.GetComponent<Rigidbody>();
            }
            if (!ownerDetector) ownerDetector = FindOwnerDetector();
            if (!vehicleRigidbody) vehicleRigidbody = transform.parent.GetComponentInParent<Rigidbody>();

            Debug.Log($"{this} {ownerDetector}");

            attachedWheelCollider = GetComponentInParent<WheelCollider>();
        }
        #endregion

        #region Internal Logics
        private GameObject FindOwnerDetector()
        {
            var objectSync = (VRCObjectSync)GetComponentInParent(typeof(VRCObjectSync));
            if (objectSync) return objectSync.gameObject;

            var rigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            if (rigidbody)
            {
                foreach (var udon in rigidbody.GetComponentsInChildren(typeof(UdonBehaviour)))
                {
                    var usharp = (UdonSharpBehaviour)udon;
                    if (!usharp) continue;
                    var name = usharp.GetUdonTypeName();
                    if (name == "SaccAirVehicle" || name == "SaccSeaVehicle" || name == "EngineController" || name == "SyncScript") return udon.gameObject;
                }
                return rigidbody.gameObject;
            }

            return gameObject;
        }
        #endregion
    }
}
