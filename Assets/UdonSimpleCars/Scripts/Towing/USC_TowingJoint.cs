
using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
#endif

namespace UdonSimpleCars
{
    [DefaultExecutionOrder(100)] // After Engine Controller
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class USC_TowingJoint : UdonSharpBehaviour
    {
        #region Public Fields
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
        #endregion

        #region Private Fields
        private bool initialized = false;
        private GameObject vehicleRoot;
        private Rigidbody parentRigidbody;
        private Vector3 center;
        private float radius;
        private USC_TowingAnchor _connectedAnchor;
        private Vector3 prevRelativePosition;
        private Collider[] colliders;
        private Vector3 jointVelocity, prevJointPosition;
        private SphereCollider trigger;
        private float reconnectableTime;
        #endregion

        #region Properties
        private USC_TowingAnchor ConnectedAnchor
        {
            set
            {
                _connectedAnchor = value;
                if (value != null)
                {
                    ConnectedAnchor_Transform = value.transform;
                    ConnectedAnchor_OwnerDetector = value.ownerDetector;
                    ConnectedAnchor_VehicleRigidbody = value.vehicleRigidbody;
                    ConnectedAnchor_AttachedWheelCollider = value.attachedWheelCollider;
                }
            }
            get => _connectedAnchor;
        }
        // Local Cache
        private Transform ConnectedAnchor_Transform;
        private GameObject ConnectedAnchor_OwnerDetector;
        private Rigidbody ConnectedAnchor_VehicleRigidbody;
        private WheelCollider ConnectedAnchor_AttachedWheelCollider;

        private bool IsAnchorOwner => isConnected && ConnectedAnchor != null;
        private bool IsVehicleOwner => isConnected && Networking.IsOwner(vehicleRoot);
        #endregion

        #region Synced Varibles
        [UdonSynced] bool isConnected;
        [UdonSynced(UdonSyncMode.Smooth)] Vector3 force;
        #endregion

        #region Unity Events
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

            if (IsAnchorOwner)
            {
                var connected = AnchorOwnerUpdate();
                if (!connected) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Disconnect));
            }
            if (IsVehicleOwner) VehicleOwnerUpdate();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ConnectedAnchor != null || Time.time < reconnectableTime) return;

            var anchor = FindAnchor();
            if (anchor == null || !Networking.IsOwner(anchor.ownerDetector)) return;

            _Connect(anchor);
        }
        #endregion

        #region Udon Events
        public override void Interact()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Disconnect));
        }
        #endregion

        #region Networked Custom Events
        public void Disconnect()
        {
            DisableTrigger();
            force = Vector3.zero;

            if (ConnectedAnchor_AttachedWheelCollider != null)
            {
                ConnectedAnchor_AttachedWheelCollider.steerAngle = 0;
            }

            isConnected = false;
            ConnectedAnchor = null;
            OnDisconnected();
        }

        public void OnConnected() => PlayOneShot(onConnected);
        public void OnDisconnected() => PlayOneShot(onDisconnected);

        #endregion

        #region Local Custom Events
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

        public void _EnableTrigger()
        {
            trigger.enabled = true;
        }
        #endregion

        #region Private Logics
        private bool AnchorOwnerUpdate()
        {
            if (!Networking.IsOwner(ConnectedAnchor_OwnerDetector)) return false;
            var jointPosition = transform.position;
            jointVelocity = (jointPosition - prevJointPosition) * Time.fixedDeltaTime;
            prevJointPosition = jointPosition;

            var connectedAnchorPositon = _connectedAnchor.transform.position;
            var relativePosition = -transform.InverseTransformPoint(connectedAnchorPositon);

            var distance = relativePosition.magnitude;
            if (distance > breakingDistance) return false;

            if (distance > wakeUpDistance * wakeUpDistance) WakeUp();

            force = relativePosition * spring + (relativePosition - prevRelativePosition) * damping;
            ConnectedAnchor_VehicleRigidbody.AddForceAtPosition(Vector3.ClampMagnitude(transform.TransformVector(force), maxAcceleration) * Time.fixedDeltaTime, connectedAnchorPositon, ForceMode.Acceleration);

            if (ConnectedAnchor_AttachedWheelCollider != null)
            {
                ConnectedAnchor_AttachedWheelCollider.steerAngle = GetSteeringAngle();
            }

            prevRelativePosition = relativePosition;

            return true;
        }

        private void VehicleOwnerUpdate()
        {
            if (force != Vector3.zero) parentRigidbody.AddForceAtPosition(Vector3.ClampMagnitude(-transform.TransformVector(force), maxAcceleration) * Time.fixedDeltaTime * massScale, transform.position, ForceMode.Acceleration);
        }

        private void DisableTrigger()
        {
            reconnectableTime = Time.time + reconnectionDelay;
            trigger.enabled = false;
            SendCustomEventDelayedSeconds(nameof(_EnableTrigger), reconnectionDelay * 0.8f);
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

        private float GetSteeringAngle()
        {
            var wheelTransform = ConnectedAnchor_AttachedWheelCollider.transform;
            var wheelUp = wheelTransform.up;
            var wheelForward = wheelTransform.forward;

            var projectedDirection = Vector3.ProjectOnPlane((ConnectedAnchor_Transform.position - transform.position).normalized, wheelUp);

            var forwardAngle = Vector3.SignedAngle(wheelForward, projectedDirection, wheelUp);
            var backwardAngle = Vector3.SignedAngle(-wheelForward, projectedDirection, wheelUp);
            return Mathf.Abs(forwardAngle) <= Mathf.Abs(backwardAngle) ? forwardAngle : backwardAngle;
        }
        #endregion

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void OnDrawGizmos()
        {
            this.UpdateProxy();
            if (!isConnected) return;

            var jointPosition = transform.position;
            var anchorPosition = ConnectedAnchor.transform.position;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(jointPosition, 0.1f);
            Gizmos.DrawWireSphere(anchorPosition, 0.1f);

            Gizmos.color = Color.white;
            Gizmos.DrawLine(jointPosition, anchorPosition);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(jointPosition, anchorPosition - force);
            Gizmos.DrawLine(anchorPosition, anchorPosition + force);
        }
#endif
    }
}
