
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
    ]
    public class USC_TugJoint : UdonSharpBehaviour
    {
        public GameObject ownerDetector;
        public ConfigurableJoint joint;
        public AudioSource audioSource;
        public AudioClip onConnected, onDisconnected;
        [HideInInspector] public Rigidbody vehicleRigidbody;
        private bool initialized = false;

        private USC_TugJoint _connectedTarget;
        private USC_TugJoint ConnectedTarget
        {
            set
            {
                _connectedTarget = value;

                if (joint != null)
                {
                    if (value == null)
                    {
                        joint.gameObject.SetActive(false);
                        joint.connectedBody = null;
                        if (initialized) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnDisconnected));
                    }
                    else
                    {
                        joint.connectedBody = value.vehicleRigidbody;
                        joint.gameObject.SetActive(true);
                        value.vehicleRigidbody.WakeUp();
                        if (initialized) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnConnected));
                    }
                }

            }
            get => _connectedTarget;
        }
        private void Start()
        {
            vehicleRigidbody = transform.parent.GetComponentInParent<Rigidbody>();
            ConnectedTarget = null;
            DisableInteractive = joint == null;
            initialized = true;
        }

        private void Update()
        {
            if (ConnectedTarget == null) return;
            if (!ConnectedTarget._IsOwner()) ConnectedTarget = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ConnectedTarget != null || joint == null || other == null) return;

            var otherJoint = other.GetComponent<USC_TugJoint>();
            if (otherJoint == null || !otherJoint._IsOwner()) return;

            ConnectedTarget = otherJoint;
        }

        public void Disconnect()
        {
            ConnectedTarget = null;
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

        public bool _IsOwner()
        {
            if (ownerDetector == null) return false;
            return Networking.IsOwner(ownerDetector.gameObject);
        }
    }
}
