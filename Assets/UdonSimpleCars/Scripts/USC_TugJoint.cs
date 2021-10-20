
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace UdonSimpleCars
{
    [
        DefaultExecutionOrder(100), // After Engine Controller
        UdonBehaviourSyncMode(/*BehaviourSyncMode.None*/ BehaviourSyncMode.NoVariableSync),
        RequireComponent(typeof(Rigidbody)),
        RequireComponent(typeof(SphereCollider))
    ]
    public class USC_TugJoint : UdonSharpBehaviour
    {
        public LayerMask anchorLayers = -1;
        public ConfigurableJoint joint;
        public AudioSource audioSource;
        public AudioClip onConnected, onDisconnected;
        [HideInInspector] public Rigidbody vehicleRigidbody;
        private bool initialized = false;

        private Vector3 center;
        private float radius;
        private USC_TugAnchor _connectedAnchor;
        private USC_TugAnchor ConnectedAnchor
        {
            set
            {
                _connectedAnchor = value;

                if (joint != null)
                {
                    if (value == null)
                    {
                        joint.gameObject.SetActive(false);
                        joint.connectedBody = null;
                    }
                    else
                    {
                        joint.connectedBody = value.vehicleRigidbody;
                        joint.gameObject.SetActive(true);
                        value.vehicleRigidbody.WakeUp();
                    }
                }
            }
            get => _connectedAnchor;
        }
        private void Start()
        {
            vehicleRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            ConnectedAnchor = null;
            DisableInteractive = joint == null;
            center = GetComponent<SphereCollider>().center;
            radius = GetComponent<SphereCollider>().radius;
            initialized = true;
        }

        private void Update()
        {
            if (ConnectedAnchor == null) return;
            if (!ConnectedAnchor._Connectable()) ConnectedAnchor = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ConnectedAnchor != null || joint == null) return;

            var anchor = FindAnchor();
            if (anchor == null || !anchor._Connectable()) return;

            _Connect(anchor);
        }

        public void _Connect(USC_TugAnchor anchor)
        {
            ConnectedAnchor = anchor;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnConnected));
        }

        public void Disconnect()
        {
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
