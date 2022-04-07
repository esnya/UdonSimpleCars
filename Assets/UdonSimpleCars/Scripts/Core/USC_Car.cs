#pragma warning disable IDE1006

using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using System.Threading;
using VRC.Udon.Common.Interfaces;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

namespace UdonSimpleCars
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VRCObjectSync))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class USC_Car : UdonSharpBehaviour
    {
        [SectionHeader("Specs")]
        public float accelerationTorque = 1.0f;
        [Range(0, 10)] public float accelerationResponse = 1f;
        public float brakeTorque = 1.0f;
        [Range(0, 10)] public float brakeResponse = 1f;
        public float maxSteeringAngle = 40.0f;
        [Range(0, 10)] public float steeringResponse = 1f;

        [SectionHeader("Sounds")]
        public AudioSource engineSound;
        public AnimationCurve engineSoundVolume = AnimationCurve.EaseInOut(0, 0.8f, 1, 1), engineSoundPitch = AnimationCurve.EaseInOut(0, 1, 1, 1.5f);

        [SectionHeader("Others")]
        public Transform steeringWheel;
        public Vector3 steeringWheelAxis = Vector3.forward;
        public float steeringWheelMaxAngle = 16 * 40;
        public GameObject operatingOnly, inVehicleOnly, driverOnly, backGearOnly, brakingOnly;

        [SectionHeader("VR Inputs")]
        [Popup("GetAxisList")] public string steeringAxis = "Oculus_CrossPlatform_SecondaryThumbstickHorizontal";
        [Popup("GetAxisList")] public string accelerationAxis = "Oculus_CrossPlatform_SecondaryIndexTrigger";
        [Popup("GetAxisList")] public string brakeAxis = "Oculus_CrossPlatform_PrimaryIndexTrigger";
        [Popup("GetAxisList")] public string backGearAxis = "Vertical";

        [SectionHeader("Keyboard Inputs")]
        public KeyCode steeringKeyLeft = KeyCode.A;
        public KeyCode steeringKeyRight = KeyCode.D;
        public KeyCode accelerationKey = KeyCode.LeftShift;
        public KeyCode backAccelerationKey = KeyCode.LeftControl;
        public KeyCode brakeKey = KeyCode.B;

        [SectionHeader("Animator Parameterss")]
        [Tooltip("Bool")] public string isOperatingParameter = "IsOperating";
        [Tooltip("Float")] public string accelerationParameter = "Acceleration";
        [Tooltip("Float")] public string brakeParameter = "Brake";
        [Tooltip("Float")] public string steeringParameter = "Steering";
        [Tooltip("Integer")] public string gearParameter = "Gear";
        [Tooltip("Bool")] public string backGearParameter = "BackGear";
        [Tooltip("Bool")] public string localIsDriverParameter = "LocalIsDriver";
        [Tooltip("Bool")] public string localInVehicleParameter = "LocalInVehicle";

        [SectionHeader("Editor")]
        public GameObject respawnerPrefab;

        [SectionHeader("Wheels")]
        [HelpBox("Managed by USC_Wheel(s)")]
        public WheelCollider[] wheels = { };
        public WheelCollider[] steeredWheels = { };
        public WheelCollider[] drivingWheels = { };
        public WheelCollider[] brakeWheels = { };
        // public WheelCollider[] parkingBrakeWheels = {};
        public WheelCollider[] detachedWheels = { };
        public Transform[] wheelVisuals = { };

        [SectionHeader("Others")]
        [Tooltip("Reparented under parent of the vehicle on Start. Resets positions on respawns.")] public Transform detachedObjects;

        private Animator animator;
        private Rigidbody vehicleRigidbody;
        private int wheelCount;
        private float[] wheelAngles;
        private Quaternion[] wheelVisualLocalRotations;
        private Vector3[] wheelVisualPositionOffsets, wheelVisualAxiesRight, wheelVisualAxiesUp;
        private bool[] wheelIsSteered;
        private Quaternion steeringWheelInitialRotation;
        private Rigidbody[] detachedRigidbodies;
        private Matrix4x4[] detachedRigidbodyTransforms;
        private VRCObjectSync[] detachedObjecySyncs;
        private float prevSpeed;
        private bool prevStartMoving;
        private bool _localIsDriver;
        private bool LocalIsDriver
        {
            set
            {
                _localIsDriver = value;
                if (driverOnly != null) driverOnly.SetActive(value);
                if (animator != null) animator.SetBool(localInVehicleParameter, value);
            }
            get => _localIsDriver;
        }

        private bool _localInVehicle;

        private bool LocalInVehicle
        {
            set
            {
                _localInVehicle = value;
                if (inVehicleOnly != null) inVehicleOnly.SetActive(value);
                if (animator != null) animator.SetBool(localInVehicleParameter, value);
            }
            get => _localInVehicle;
        }
        [UdonSynced(UdonSyncMode.Smooth)] private float wheelSpeed;
        [UdonSynced(UdonSyncMode.Smooth), FieldChangeCallback(nameof(AccelerationValue))] private float _accelerationValue;
        private float AccelerationValue
        {
            set
            {
                _accelerationValue = value;

                if (IsOperating)
                {
                    var absoluteValue = Mathf.Abs(value);
                    if (engineSound != null)
                    {
                        engineSound.pitch = engineSoundPitch.Evaluate(absoluteValue);
                        engineSound.volume = engineSoundVolume.Evaluate(absoluteValue);
                    }

                    if (animator != null)
                    {
                        animator.SetFloat(accelerationParameter, absoluteValue);
                    }
                }

                if (LocalIsDriver)
                {
                    foreach (var wheel in drivingWheels) wheel.motorTorque = value * accelerationTorque / drivingWheels.Length;
                }
            }
            get => _accelerationValue;
        }
        [UdonSynced(UdonSyncMode.Smooth), FieldChangeCallback(nameof(BrakeValue))] private float _brakeValue;
        private float BrakeValue
        {
            set
            {
                _brakeValue = value;

                if (animator != null && IsOperating)
                {
                    animator.SetFloat(brakeParameter, value);
                }

                if (brakingOnly != null) brakingOnly.SetActive(value > 0);

                if (LocalIsDriver)
                {
                    foreach (var wheel in brakeWheels) wheel.brakeTorque = value * brakeTorque / brakeWheels.Length;
                }
            }
            get => _brakeValue;
        }
        [UdonSynced(UdonSyncMode.Smooth), FieldChangeCallback(nameof(SteeringValue))] private float _steeringValue;
        private float SteeringValue
        {
            set
            {
                _steeringValue = value;

                if (animator != null && IsOperating)
                {
                    animator.SetFloat(steeringParameter, value * 0.5f + 0.5f);
                }

                if (LocalIsDriver)
                {
                    foreach (var wheel in steeredWheels) wheel.steerAngle = value * maxSteeringAngle / steeredWheels.Length;
                }
            }
            get => _steeringValue;
        }
        [UdonSynced, FieldChangeCallback(nameof(IsOperating))] private bool _isOperating;
        private bool IsOperating
        {
            set
            {
                _isOperating = value;
                if (operatingOnly != null) operatingOnly.SetActive(value);
                if (engineSound != null) engineSound.gameObject.SetActive(value);
                if (animator != null) animator.SetBool(isOperatingParameter, value);
            }
            get => _isOperating;
        }

        const int GEAR_PARKING = -2;
        const int GEAR_BACK = -1;
        const int GEAR_NEUTRAL = 0;
        const int GEAR_DRIVE = 1;
        [UdonSynced, FieldChangeCallback(nameof(Gear))] private int _gear = GEAR_DRIVE;
        private int Gear
        {
            set
            {
                _gear = value;

                if (backGearOnly != null) backGearOnly.SetActive(value == GEAR_BACK);

                if (animator != null)
                {
                    animator.SetInteger(gearParameter, value);
                    animator.SetBool(backGearParameter, value == GEAR_BACK);
                }
            }
            get => _gear;
        }

        private bool BackGear
        {
            set => Gear = value ? GEAR_BACK : GEAR_DRIVE;
            get => Gear == GEAR_BACK;
        }

        private void Start()
        {
            vehicleRigidbody = GetComponent<Rigidbody>();
            animator = GetComponentInParent<Animator>();

            wheelCount = wheels.Length;
            wheelAngles = new float[wheelCount];
            wheelVisualLocalRotations = new Quaternion[wheelCount];
            wheelVisualPositionOffsets = new Vector3[wheelCount];
            wheelVisualAxiesRight = new Vector3[wheelCount];
            wheelVisualAxiesUp = new Vector3[wheelCount];
            wheelIsSteered = new bool[wheelCount];

            for (var i = 0; i < wheelCount; i++)
            {
                var wheel = wheels[i];
                var wheelTransform = wheel.transform;

                var visual = (wheelVisuals != null && i < wheelVisuals.Length) ? wheelVisuals[i] : null;
                if (visual != null)
                {
                    wheelVisualPositionOffsets[i] = wheelTransform.InverseTransformPoint(visual.position) + Vector3.up * wheel.suspensionDistance * wheel.suspensionSpring.targetPosition;
                    wheelVisualLocalRotations[i] = visual.localRotation;
                    wheelVisualAxiesRight[i] = visual.InverseTransformDirection(wheelTransform.right);
                    wheelVisualAxiesUp[i] = visual.InverseTransformDirection(wheelTransform.up);
                }

                wheelIsSteered[i] = IsWheelSteered(wheel);
            }

            if (steeringWheel) steeringWheelInitialRotation = steeringWheel.localRotation;

            if (engineSound != null)
            {
                engineSound.playOnAwake = true;
                engineSound.loop = true;
            }

            LocalIsDriver = false;
            LocalInVehicle = false;
            AccelerationValue = 0;
            BrakeValue = 1.0f;
            SteeringValue = 0;
            IsOperating = false;
            Gear = GEAR_DRIVE;

            if (detachedObjects)
            {
                detachedObjects.name = $"{gameObject.name}_{detachedObjects.name}";
                detachedObjects.SetParent(transform.parent, true);
                detachedRigidbodies = detachedObjects.GetComponentsInChildren<Rigidbody>();
                detachedRigidbodyTransforms = new Matrix4x4[detachedRigidbodies.Length];
                for (var i = 0; i < detachedRigidbodies.Length; i++)
                {
                    var rigidbody = detachedRigidbodies[i];
                    detachedRigidbodyTransforms[i] = transform.worldToLocalMatrix * rigidbody.transform.localToWorldMatrix;
                }

                detachedObjecySyncs = (VRCObjectSync[])detachedObjects.GetComponentsInChildren(typeof(VRCObjectSync), true);
                detachedWheels = detachedObjects.GetComponentsInChildren<WheelCollider>(true);
            }
        }

        private void Update()
        {
            if (LocalIsDriver) DriverUpdate();
            LocalUpdate();
        }

        private void DriverUpdate()
        {
            var accelerationInput = Input.GetKey(backAccelerationKey) ? -1.0f : (Input.GetKey(accelerationKey) ? 1.0f : (Input.GetAxisRaw(accelerationAxis) * (BackGear ? -1.0f : 1.0f)));
            var steeringInput = Input.GetKey(steeringKeyLeft) ? -1.0f : (Input.GetKey(steeringKeyRight) ? 1.0f : Input.GetAxisRaw(steeringAxis));
            var brakeInput = Input.GetKey(brakeKey) ? 1.0f : Input.GetAxisRaw(brakeAxis);

            var backGearInput = Input.GetAxisRaw(backGearAxis);
            if (Mathf.Abs(backGearInput) > 0.5f) BackGear = backGearInput < -0.5f;

            var deltaTime = Time.deltaTime;

            AccelerationValue = LinearLerp(AccelerationValue, accelerationInput, accelerationResponse * deltaTime, -1.0f, 1.0f);
            BrakeValue = (brakeInput < 0.1f) ? 0.0f : LinearLerp(BrakeValue, brakeInput, brakeResponse * deltaTime, 0.0f, 1.0f);
            SteeringValue = LinearLerp(SteeringValue, steeringInput, steeringResponse * deltaTime, -1.0f, 1.0f);

            foreach (var wheel in steeredWheels) wheel.steerAngle = SteeringValue * maxSteeringAngle;
            foreach (var wheel in drivingWheels) wheel.motorTorque = AccelerationValue * accelerationTorque / drivingWheels.Length;
            foreach (var wheel in brakeWheels) wheel.brakeTorque = BrakeValue * brakeTorque;

            wheelSpeed = CalculateWheelSpeed();

            var speed = vehicleRigidbody.velocity.magnitude;
            var startMoving = Mathf.Approximately(prevSpeed, 0) && speed > 0;
            if (startMoving)
            {
                SetDetachedWheelsMotorTorque(1.0e-36f);
            }
            else if (prevStartMoving)
            {
                SetDetachedWheelsMotorTorque(0);
            }
            prevSpeed = speed;
            prevStartMoving = startMoving;
        }

        private void LocalUpdate()
        {
            if (!IsOperating) return;

            if (steeringWheel)
            {
                steeringWheel.localRotation = steeringWheelInitialRotation * Quaternion.AngleAxis(SteeringValue * steeringWheelMaxAngle, steeringWheelAxis);
            }

            for (var i = 0; i < wheelVisuals.Length; i++)
            {
                var visual = wheelVisuals[i];
                if (visual == null) continue;

                var wheel = wheels[i];
                var wheelTransform = wheel.transform;

                Vector3 position;
                Quaternion rotation;
                wheel.GetWorldPose(out position, out rotation);
                visual.position = wheelTransform.TransformPoint(wheelTransform.InverseTransformPoint(position) + wheelVisualPositionOffsets[i]);

                var wheelAngle = wheelAngles[i] + (LocalIsDriver ? wheel.rpm : wheelSpeed * wheel.radius) * Time.deltaTime * (360.0f / 60.0f);
                wheelAngles[i] = wheelAngle;

                var steeringRotation = wheelIsSteered[i] ? Quaternion.AngleAxis(SteeringValue * maxSteeringAngle, wheelVisualAxiesUp[i]) : Quaternion.identity;
                visual.localRotation = wheelVisualLocalRotations[i] * steeringRotation * Quaternion.AngleAxis(wheelAngle, wheelVisualAxiesRight[i]);
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                if (detachedObjects)
                {
                    foreach (var sync in detachedObjecySyncs) Networking.SetOwner(player, sync.gameObject);
                }
            }
        }

        public void OnRespawned()
        {
            foreach (var rigidbody in GetComponentsInChildren<Rigidbody>())
            {
                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            if (detachedRigidbodies != null)
            {
                for (var i = 0; i < detachedRigidbodies.Length; i++)
                {
                    var rigidbody = detachedRigidbodies[i];
                    var m = transform.localToWorldMatrix * detachedRigidbodyTransforms[i];
                    rigidbody.position = m.MultiplyPoint3x4(Vector3.zero);
                    rigidbody.rotation = m.rotation;
                    rigidbody.velocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }
            }
        }


        public void _OnEnteredAsDriver()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            LocalIsDriver = true;
            LocalInVehicle = true;

            BackGear = false;
            IsOperating = true;

        }

        public void _OnEnteredAsPassenger()
        {
            LocalInVehicle = true;
        }

        public void _OnExited()
        {
            if (LocalIsDriver)
            {
                AccelerationValue = 0;
                LocalIsDriver = false;
                IsOperating = false;

                SetDetachedWheelsMotorTorque(0);
            }
            LocalInVehicle = false;
        }

        public void _Respawn()
        {
            if (IsOperating) return;

            LocalIsDriver = true;
            IsOperating = true;

            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            var objectSync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
            objectSync.Respawn();

            BackGear = false;
            AccelerationValue = 0;
            SteeringValue = 0;
            BrakeValue = 1;

            LocalIsDriver = false;
            IsOperating = false;

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnRespawned));
        }

        private void SetDetachedWheelsMotorTorque(float value)
        {
            if (!detachedObjects) return;
            foreach (var wheel in detachedWheels) wheel.motorTorque = value;
        }

        private float LinearLerp(float currentValue, float targetValue, float speed, float minValue, float maxValue)
        {
            return Mathf.Clamp(Mathf.MoveTowards(currentValue, targetValue, speed), minValue, maxValue);
        }

        private float CalculateWheelSpeed()
        {
            var result = 0.0f;
            foreach (var wheel in wheels) result += wheel.rpm / (wheel.radius * wheelCount);
            return result;
        }

        private bool IsWheelSteered(WheelCollider wheel)
        {
            foreach (var steeredWheel in steeredWheels)
            {
                if (steeredWheel == wheel) return true;
            }
            return false;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void Reset()
        {
            GetComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;
        }

        private string[] GetAxisList() => new[] {
            "Oculus_CrossPlatform_PrimaryIndexTrigger",
            "Oculus_CrossPlatform_SecondaryIndexTrigger",
            "Oculus_CrossPlatform_PrimaryHandTrigger",
            "Oculus_CrossPlatform_SecondaryHandTrigger",
            "Horizontal",
            "Oculus_CrossPlatform_SecondaryThumbstickHorizontal",
            "Vertical",
            "Oculus_CrossPlatform_SecondaryThumbstickVertical",
        };
        private string[] GetButtonList() => new[] {
            "Oculus_CrossPlatform_PrimaryThumbstick",
            "Oculus_CrossPlatform_SecondaryThumbstick",
            "Oculus_CrossPlatform_Button4",
            "Oculus_CrossPlatform_Button2",
        };


        [Button("Add Respawner", true)]
        public void AddRespawner()
        {
            var respawner = Instantiate(respawnerPrefab);
            respawner.transform.parent = transform.parent;
            respawner.transform.position = transform.TransformPoint(respawnerPrefab.transform.localPosition);

            respawner.name = respawner.name.Replace("(Clone)", $"({gameObject.name})");

            var respawnerUdon = respawner.GetUdonSharpComponent<USC_Respawner>();
            respawnerUdon.target = this;
            respawnerUdon.ApplyProxyModifications();

            Undo.RegisterCreatedObjectUndo(respawner, "Add Respawner");
        }
#endif
    }
}
