using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VRCPickup))]
    public class USC_RemotePusher : UdonSharpBehaviour
    {
        public float maxForce = 80.0f;
        public float spring = 10000.0f;
        public float wakeUpThroshold = 1f;

        private Rigidbody attachedRigidbody;
        private GameObject attachedGameObject;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private bool isHeld;
        [UdonSynced] private Vector3 force;
        private WheelCollider[] wheelColliders;

        private Vector3 AnchorPosition => transform.parent.TransformPoint(localPosition);

        bool _isSleeping = true;
        private bool IsSleeping
        {
            set {
                if (value != _isSleeping)
                {
                    foreach (var wheel in wheelColliders) wheel.motorTorque += value ? 2.0e-36f : -2.0e-36f;
                }
                _isSleeping = value;
            }
        }

        private void Reset()
        {
            GetComponent<Rigidbody>().isKinematic = true;
        }

        private void Start()
        {
            attachedRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            attachedGameObject = attachedRigidbody.gameObject;
            wheelColliders = attachedRigidbody.GetComponentsInChildren<WheelCollider>(true);
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
        }

        public override void OnPickup() => SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RemotePickup));
        public void RemotePickup() => isHeld = true;

        public override void OnDrop() => SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RemoteDrop));
        public void RemoteDrop()
        {
            isHeld = false;

            force = Vector3.zero;
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;

            IsSleeping = false;
        }

        public void FixedUpdate()
        {
            if (isHeld && Networking.IsOwner(attachedGameObject))
            {
                attachedRigidbody.AddForceAtPosition(force, AnchorPosition);

                IsSleeping = force.magnitude < wakeUpThroshold;
            }
        }

        public void Update()
        {
            if (isHeld && Networking.IsOwner(gameObject))
            {
                var relative = transform.position - AnchorPosition;
                force = Vector3.ClampMagnitude(relative * spring, maxForce);
            }
        }
    }
}
