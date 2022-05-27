using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VRCPickup))]
    public class USC_HandPusher : UdonSharpBehaviour
    {
        public float maxForce = 80.0f;
        public float spring = 10000.0f;
        public float wakeUpDistance = 0.1f;
        public bool sync = true;
        private Rigidbody attachedRigidbody;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private bool pickup;
        private WheelCollider[] wheelColliders;

        private void Reset()
        {
            GetComponent<Rigidbody>().isKinematic = true;
        }

        private void Start()
        {
            attachedRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            wheelColliders = attachedRigidbody.GetComponentsInChildren<WheelCollider>(true);
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
        }

        public override void OnPickup()
        {
            pickup = true;
            if (sync) Networking.SetOwner(Networking.LocalPlayer, attachedRigidbody.gameObject);
        }

        public override void OnDrop()
        {
            pickup = false;
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;

            foreach (var wheel in wheelColliders) wheel.motorTorque = 0;
        }

        public void FixedUpdate()
        {
            if (pickup)
            {
                var anchorPosition = transform.parent.TransformPoint(localPosition);
                var relative = transform.position - anchorPosition;
                var force = Vector3.ClampMagnitude(relative * spring, maxForce);
                attachedRigidbody.AddForceAtPosition(force, anchorPosition);

                if (relative.magnitude > wakeUpDistance)
                {
                    foreach (var wheel in wheelColliders) wheel.motorTorque = 1.0E-32f;
                }
            }
        }
    }
}
