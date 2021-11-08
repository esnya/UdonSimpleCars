
using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace UdonSimpleCars
{
    [
        DefaultExecutionOrder(100), // After Engine Controller
        UdonBehaviourSyncMode(BehaviourSyncMode.Continuous),
        RequireComponent(typeof(Rigidbody)),
        RequireComponent(typeof(SphereCollider))
    ]
    public class USC_TowingJoint : UdonSharpBehaviour
    {
        [SectionHeader("Anchor")]
        public LayerMask anchorLayers = -1;
        public float spring = 500.0f;
        public float damping = 100000.0f;
        public float maxAcceleration = 2000.0f;
        public float massScale = 1.0f;
        public float wakeUpDistance = 1.0f;
        public float breakingDistance = 10.0f;
        public float reconnectionDelay = 5.0f;

        [SectionHeader("Sounds")]
        public AudioSource audioSource;
        public AudioClip onConnected, onDisconnected;

        private bool initialized = false;
        private GameObject vehicleRoot;
        private Rigidbody parentRigidbody;
        private Vector3 center;
        private float radius;
        private USC_TowingAnchor _connectedAnchor;
        private Vector3 prevRelativePosition;
        private Collider[] colliders;
        private GameObject anchorOwnerDetector;
        private Rigidbody anchorRigidbody;
        private USC_TowingAnchor ConnectedAnchor
        {
            set
            {
                _connectedAnchor = value;
                anchorOwnerDetector = value == null ? null : value.ownerDetector;
                anchorRigidbody = value == null ? null : value.vehicleRigidbody;
            }
            get => _connectedAnchor;
        }
        [UdonSynced] bool isConnected;
        [UdonSynced(UdonSyncMode.Smooth)] Vector3 force;

        private Vector3 jointVelocity, prevJointPosition;
        private SphereCollider trigger;

        private void Start()
        {
            ConnectedAnchor = null;

            parentRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            var objectSync = (VRCObjectSync)GetComponentInParent(typeof(VRCObjectSync));
            if (objectSync != null) vehicleRoot = objectSync.gameObject;
            if (vehicleRoot == null) vehicleRoot = gameObject;

            trigger = GetComponent<SphereCollider>();
            center = trigger.center;
            radius = trigger.radius;
            initialized = true;
        }

        private void FixedUpdate()
        {
            if (!isConnected) return;

            if (ConnectedAnchor != null)
            {
                var connectable = Networking.IsOwner(anchorOwnerDetector);
                if (connectable)
                {
                    var jointPosition = transform.position;
                    jointVelocity = (jointPosition - prevJointPosition) * Time.fixedDeltaTime;
                    prevJointPosition = jointPosition;

                    var connectedAnchorPositon = _connectedAnchor.transform.position;
                    var relativePosition = -transform.InverseTransformPoint(connectedAnchorPositon);

                    var distance = relativePosition.magnitude;
                    if (distance > breakingDistance) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Disconnect));
                    else if (distance > wakeUpDistance * wakeUpDistance) WakeUp();
                    else
                    {
                        force = relativePosition * spring + (relativePosition - prevRelativePosition) * damping;
                        anchorRigidbody.AddForceAtPosition(Vector3.ClampMagnitude(transform.TransformVector(force), maxAcceleration) * Time.fixedDeltaTime, connectedAnchorPositon, ForceMode.Acceleration);
                    }

                    prevRelativePosition = relativePosition;
                }
                else
                {
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Disconnect));
                }
            }

            if (Networking.IsOwner(vehicleRoot))
            {
                if (force != Vector3.zero) parentRigidbody.AddForceAtPosition(Vector3.ClampMagnitude(-transform.TransformVector(force), maxAcceleration) * Time.fixedDeltaTime * massScale, transform.position, ForceMode.Acceleration);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ConnectedAnchor != null) return;

            var anchor = FindAnchor();
            if (anchor == null || !Networking.IsOwner(anchor.ownerDetector)) return;

            _Connect(anchor);
        }

        public void _Connect(USC_TowingAnchor anchor)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            force = Vector3.zero;
            prevRelativePosition = Vector3.zero;
            prevJointPosition = transform.position;

            ConnectedAnchor = anchor;
            isConnected = true;
            WakeUp();
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnConnected));
        }

        public void Disconnect()
        {
            DisableTrigger();
            force = Vector3.zero;
            isConnected = false;
            ConnectedAnchor = null;
            OnDisconnected();
        }

        public override void Interact()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Disconnect));
        }

        public void OnConnected() => PlayOneShot(onConnected);
        public void OnDisconnected() => PlayOneShot(onDisconnected);

        public void _EnableTrigger()
        {
            trigger.enabled = true;
        }

        private void DisableTrigger()
        {
            trigger.enabled = false;
            SendCustomEventDelayedSeconds(nameof(_EnableTrigger), reconnectionDelay);
        }

        private void WakeUp()
        {
            var vehicleRigidbody = ConnectedAnchor.vehicleRigidbody;
            var forward = vehicleRigidbody.transform.forward;
            vehicleRigidbody.velocity = forward * Mathf.Sign(Vector3.Dot(forward, transform.position - _connectedAnchor.transform.position)) * .5f;
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip);
        }

        private USC_TowingAnchor FindAnchor()
        {
            var colliders = Physics.OverlapSphere(transform.TransformPoint(center), radius, anchorLayers, QueryTriggerInteraction.Collide);

            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                var anchor = collider.GetComponent<USC_TowingAnchor>();
                if (anchor != null) return anchor;
            }

            return null;
        }
    }
}
