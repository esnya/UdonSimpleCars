
using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace UdonSimpleCars
{
    [
        DefaultExecutionOrder(100), // After Engine Controller
        UdonBehaviourSyncMode(BehaviourSyncMode.Continuous),
        RequireComponent(typeof(Rigidbody)),
        RequireComponent(typeof(SphereCollider))
    ]
    public class USC_TugJoint : UdonSharpBehaviour
    {
        [SectionHeader("Anchor")]
        public LayerMask anchorLayers = -1;
        public float spring = 100.0f;
        public float damping = 10.0f;
        public float maxAcceleration = 50.0f;

        [SectionHeader("Sounds")]
        public AudioSource audioSource;
        public AudioClip onConnected, onDisconnected;

        private bool initialized = false;
        private GameObject vehicleRoot;
        private Rigidbody parentRigidbody;
        private Vector3 center;
        private float radius;
        private USC_TugAnchor _connectedAnchor;
        private Vector3 prevRelativePosition;
        private USC_TugAnchor ConnectedAnchor
        {
            set
            {
                _connectedAnchor = value;
                if (value != null) value._WakeUp();
            }
            get => _connectedAnchor;
        }
        [UdonSynced] bool isConnected;
        [UdonSynced(UdonSyncMode.Smooth)] Vector3 force;

        private void Start()
        {
            ConnectedAnchor = null;

            parentRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            var objectSync = (VRCObjectSync)GetComponentInParent(typeof(VRCObjectSync));
            if (objectSync != null) vehicleRoot = objectSync.gameObject;
            if (vehicleRoot == null) vehicleRoot = gameObject;

            center = GetComponent<SphereCollider>().center;
            radius = GetComponent<SphereCollider>().radius;
            initialized = true;
        }

        private void Update()
        {
            if (!isConnected) return;

            if (ConnectedAnchor != null)
            {
                if (ConnectedAnchor._IsConnectable())
                {
                    var connectedAnchorPositon = _connectedAnchor.transform.position;
                    var relativePosition = transform.position - connectedAnchorPositon;
                    force = Vector3.ClampMagnitude(relativePosition * spring - (relativePosition - prevRelativePosition) * damping, maxAcceleration);
                    prevRelativePosition = relativePosition;
                    ConnectedAnchor.vehicleRigidbody.AddForceAtPosition(force, connectedAnchorPositon, ForceMode.Acceleration);
                }
                else
                {
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Disconnect));
                }
            }

            if (Networking.IsOwner(vehicleRoot))
            {
                parentRigidbody.AddForceAtPosition(force, transform.position, ForceMode.Acceleration);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ConnectedAnchor != null) return;

            var anchor = FindAnchor();
            if (anchor == null || !anchor._IsConnectable()) return;

            _Connect(anchor);
        }

        public void _Connect(USC_TugAnchor anchor)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            force = Vector3.zero;
            prevRelativePosition = Vector3.zero;

            ConnectedAnchor = anchor;
            isConnected = true;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnConnected));
        }

        public void Disconnect()
        {
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

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip);
        }

        private USC_TugAnchor FindAnchor()
        {
            var colliders = Physics.OverlapSphere(transform.TransformPoint(center), radius, anchorLayers, QueryTriggerInteraction.Collide);

            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                var anchor = collider.GetComponent<USC_TugAnchor>();
                if (anchor != null) return anchor;
            }

            return null;
        }
    }
}
