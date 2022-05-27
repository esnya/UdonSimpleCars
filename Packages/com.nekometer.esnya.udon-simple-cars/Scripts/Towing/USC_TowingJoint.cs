using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
#endif

namespace UdonSimpleCars
{
    [DefaultExecutionOrder(-100)] // Before USC_Car
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [RequireComponent(typeof(AudioSource))]
    public class USC_TowingJoint : UdonSharpBehaviour
    {
        public LayerMask anchorLayers = 1 | 1 << 17;
        public float connectingMaxDistance = 0.5f;

        public float maxAcceleration = 20.0f;
        public float spring = 10.0f;
        public float damper = 10.0f;
        public float minSteeringSpeed = 1.0f;
        public float breakingDistance = 10.0f;
        public float reconnectionDelay = 10;
        public float wakeUpDistance = 0.2f;
        public string[] keywords = { "DEFAULT" };
        public bool syncOwnership = false;

        [Space]
        public AudioClip onConnectedSound;
        public AudioClip onDisconnectedSound;

        private AudioSource audioSource;
        private Rigidbody attachedRigidbody;

        private GameObject ownerDetector;
        private Rigidbody connectedRigidbody;
        private Transform connectedTransform;
        private WheelCollider connectedWheelCollider;
        private USC_TowingAnchor _connectedAnchor;
        private USC_TowingAnchor ConnectedAnchor
        {
            set
            {
                if (!value && connectedWheelCollider)
                {
                    connectedWheelCollider.steerAngle = 0;
                }
                _connectedAnchor = value;

                Connected = value != null;
                connectedTransform = value ? value.transform : null;

                ownerDetector = value ? value.ownerDetector : null;
                connectedRigidbody = value ? value.vehicleRigidbody : null;
                connectedWheelCollider = value ? value.attachedWheelCollider : null;
            }
            get => _connectedAnchor;
        }

        private float reconnectableTime;
        [UdonSynced][FieldChangeCallback(nameof(Connected))] private bool _connected;
        private bool Connected
        {
            set
            {
                if (value != _connected)
                {
                    if (value)
                    {
                        OnConnected();
                    }
                    else
                    {
                        OnDisconnected();
                    }
                }
                _connected = value;
            }
            get => _connected;
        }

        [UdonSynced][FieldChangeCallback(nameof(ConnectedMass))] private float _connectedMass;
        private SphereCollider trigger;
        private Vector3 prevPosition;
        private Vector3 prevVelocity;
        private readonly float prevSpeed;
        private readonly bool prevStartMoving;
        private bool prevWakeUp;

        private float ConnectedMass
        {
            set => _connectedMass = value;
            get => _connectedMass;
        }

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            attachedRigidbody = GetComponentInParent<Rigidbody>();
            trigger = GetComponent<SphereCollider>();

            ConnectedAnchor = null;
        }

        private void FixedUpdate()
        {
            if (ConnectedAnchor != null)
            {
                Vector3 anchorPosition = connectedTransform.position;
                Vector3 anchorToJoint = anchorPosition - transform.position;

                float anchorDistance = anchorToJoint.magnitude;
                if (!Networking.IsOwner(ownerDetector) || anchorDistance > breakingDistance)
                {
                    Disconnect();
                }
                else
                {
                    Vector3 jointVelocity = attachedRigidbody.velocity;
                    Vector3 connectedVelocity = connectedRigidbody.velocity;
                    Vector3 connectedToJoint = jointVelocity - connectedVelocity;
                    connectedRigidbody.AddForceAtPosition(Vector3.ClampMagnitude((connectedToJoint * damper) - (anchorToJoint * spring), maxAcceleration), anchorPosition, ForceMode.Acceleration);

                    bool wakeUp = !prevWakeUp && connectedToJoint.magnitude > wakeUpDistance;
                    if (wakeUp)
                    {
                        SetConnectedWheelsMotorTorque(1.0e-36f);
                    }
                    if (prevWakeUp)
                    {
                        SetConnectedWheelsMotorTorque(0.0f);
                    }
                    prevWakeUp = wakeUp;

                    if (connectedWheelCollider)
                    {
                        connectedWheelCollider.steerAngle = Vector3.SignedAngle(connectedRigidbody.transform.forward, transform.forward, Vector3.up);
                    }
                }
            }
            else if (Connected && Networking.IsOwner(attachedRigidbody.gameObject))
            {
                var deltaTime = Time.fixedDeltaTime;
                var position = transform.position;
                var velocity = (position - prevPosition) / deltaTime;
                var acceleration = (velocity - prevVelocity) / deltaTime;
                prevPosition = position;
                prevVelocity = velocity;

                attachedRigidbody.AddForceAtPosition(Vector3.ClampMagnitude(acceleration * (ConnectedMass / attachedRigidbody.mass), maxAcceleration), position, ForceMode.Acceleration);
            }
        }

        private bool Connectable => !Connected && Time.time >= reconnectableTime;

        public void _TryConnect()
        {
            if (!Connectable)
            {
                return;
            }

            foreach (Collider collider in Physics.OverlapSphere(transform.position, connectingMaxDistance, anchorLayers))
            {
                if (!collider || !collider.attachedRigidbody)
                {
                    continue;
                }

                USC_TowingAnchor anchor = collider.GetComponent<USC_TowingAnchor>();
                if (anchor)
                {
                    Connect(anchor);
                    return;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other || !Connectable)
            {
                return;
            }

            Rigidbody targetRigidbody = other.attachedRigidbody;
            if (!targetRigidbody)
            {
                return;
            }

            USC_TowingAnchor targetAnchor = other.GetComponent<USC_TowingAnchor>();
            if (!targetAnchor) return;

            Connect(targetAnchor);
        }

        public override void Interact()
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Disconnect));
        }

        private void SetConnectedWheelsMotorTorque(float value)
        {
            if (!connectedRigidbody)
            {
                return;
            }

            foreach (WheelCollider wheel in connectedRigidbody.GetComponentsInChildren<WheelCollider>())
            {
                wheel.motorTorque = value;
            }
        }

        private bool MatchKeywords(string[] a, string[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0) return true;
            foreach (var c in a)
            {
                foreach (var d in b)
                {
                    if (c == d) return true;
                }
            }
            return false;
        }

        private void Connect(USC_TowingAnchor targetAnchor)
        {
            if (targetAnchor.vehicleRigidbody == attachedRigidbody || !MatchKeywords(keywords, targetAnchor.keywords)) return;

            if (syncOwnership)
            {
                if (!Networking.IsOwner(gameObject)) return;
                Networking.SetOwner(Networking.LocalPlayer, targetAnchor.vehicleRigidbody.gameObject);
            }
            else
            {
                if (!Networking.IsOwner(targetAnchor.ownerDetector)) return;
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            ConnectedAnchor = targetAnchor;
            RequestSerialization();
        }

        public void Disconnect()
        {
            ConnectedAnchor = null;
            reconnectableTime = Time.time + reconnectionDelay;
            trigger.enabled = false;
            SendCustomEventDelayedSeconds(nameof(_ReActivate), reconnectionDelay * 0.5f);
            RequestSerialization();
        }

        public void _ReActivate()
        {
            trigger.enabled = true;
        }

        private void OnConnected()
        {
            PlayOneShot(onConnectedSound);
        }

        private void OnDisconnected()
        {
            PlayOneShot(onDisconnectedSound);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
