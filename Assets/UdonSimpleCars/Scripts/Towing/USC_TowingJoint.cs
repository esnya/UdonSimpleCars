using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
#endif

namespace UdonSimpleCars
{
    [DefaultExecutionOrder(-100)] // Before USC_Car
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(AudioSource))]
    public class USC_TowingJoint : UdonSharpBehaviour
    {
        public float maxAcceleration = 20.0f;
        public float spring = 10.0f;
        public float damper = 10.0f;
        public float minSteeringSpeed = 1.0f;
        public float breakingDistance = 10.0f;
        public float reconnectionDelay = 10;
        public float wakeUpDistance = 0.2f;
        public float fakeMassResponse = 1;

        [Space]
        public AudioClip onConnectedSound;
        public AudioClip onDisconnectedSound;

        private AudioSource audioSource;
        private Rigidbody jointRigidbody;
        private GameObject vehicleRoot;
        private float initialJointMass;

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

                ConnectedMass = connectedRigidbody ? connectedRigidbody.mass : initialJointMass;
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
                    if (value) OnConnected();
                    else OnDisconnected();
                }
                _connected = value;
            }
            get => _connected;
        }

        [UdonSynced][FieldChangeCallback(nameof(ConnectedMass))] private float _connectedMass;
        private SphereCollider trigger;
        private Vector3 prevPosition;
        private float prevSpeed;
        private bool prevStartMoving;
        private bool prevWakeUp;

        private float ConnectedMass
        {
            set
            {
                _connectedMass = value;
            }
            get => _connectedMass;
        }

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            jointRigidbody = GetComponent<Rigidbody>();
            vehicleRoot = GetComponentInParent<USC_Car>().gameObject;
            trigger = GetComponent<SphereCollider>();

            initialJointMass = jointRigidbody.mass;

            ConnectedAnchor = null;
        }

        private void FixedUpdate()
        {
            if (ConnectedAnchor != null)
            {
                var anchorPosition = connectedTransform.position;
                var anchorToJoint = anchorPosition - transform.position;

                var anchorDistance = anchorToJoint.magnitude;
                if (!Networking.IsOwner(ownerDetector) || anchorDistance > breakingDistance)
                {
                    Disconnect();
                }
                else
                {
                    var jointVelocity = jointRigidbody.velocity;
                    var connectedVelocity = connectedRigidbody.velocity;
                    var connectedToJoint = jointVelocity - connectedVelocity;
                    connectedRigidbody.AddForceAtPosition(Vector3.ClampMagnitude(connectedToJoint * damper - anchorToJoint * spring, maxAcceleration), anchorPosition, ForceMode.Acceleration);

                    var wakeUp = !prevWakeUp && connectedToJoint.magnitude > wakeUpDistance;
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

                var currentJointMass = jointRigidbody.mass;
                if (!Mathf.Approximately(currentJointMass, ConnectedMass))
                {
                    jointRigidbody.mass = Mathf.Lerp(currentJointMass, ConnectedMass, fakeMassResponse * Time.fixedDeltaTime);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other || Connected || Time.time < reconnectableTime) return;
            var targetRigidbody = other.attachedRigidbody;
            if (!targetRigidbody) return;

            var targetAnchor = other.GetComponent<USC_TowingAnchor>();
            if (!targetAnchor || !Networking.IsOwner(targetAnchor.ownerDetector)) return;

            Connect(targetAnchor);
        }

        public override void Interact()
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Disconnect));
        }

        private void SetConnectedWheelsMotorTorque(float value)
        {
            if (!connectedRigidbody) return;
            foreach (var wheel in connectedRigidbody.GetComponentsInChildren<WheelCollider>()) wheel.motorTorque = value;
        }

        private void Connect(USC_TowingAnchor targetAncor)
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            ConnectedAnchor = targetAncor;
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
            jointRigidbody.mass = initialJointMass;
            PlayOneShot(onDisconnectedSound);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip) audioSource.PlayOneShot(clip);
        }
    }
}
