
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

namespace UdonSimpleCars
{
    [
        UdonBehaviourSyncMode(/*BehaviourSyncMode.None*/ BehaviourSyncMode.NoVariableSync),
    ]
    public class USC_TowingAnchor : UdonSharpBehaviour
    {
        [Tooltip("Default: VRCObjectSync in parent")] public GameObject ownerDetector;
        [HideInInspector] public Rigidbody vehicleRigidbody;

        private void Start()
        {
            var objectSync = (VRCObjectSync)GetComponentInParent(typeof(VRCObjectSync));
            if (ownerDetector == null && objectSync != null) ownerDetector = objectSync.gameObject;

            vehicleRigidbody = objectSync != null ? objectSync.GetComponent<Rigidbody>() : null;
            if (vehicleRigidbody == null) vehicleRigidbody = GetComponentInParent<Rigidbody>();
        }
    }
}
