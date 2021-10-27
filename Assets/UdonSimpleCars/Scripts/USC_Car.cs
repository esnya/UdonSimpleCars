#pragma warning disable IDE1006

using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using System.Threading;

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
        public AnimationCurve engineSoundPitch = AnimationCurve.Linear(0, 1.0f, 1, 1.5f), engineSoundVolume = AnimationCurve.Linear(0, 0.8f, 1, 1.0f);

        [SectionHeader("Others")]
        public Transform steeringWheel;
        public Vector3 steeringWheelAxis = Vector3.forward;
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
        public WheelCollider[] wheels = {};
        public WheelCollider[] steeredWheels = {};
        public WheelCollider[] drivingWheels = {};
        public WheelCollider[] brakeWheels = {};
        // public WheelCollider[] parkingBrakeWheels = {};
        public Transform[] wheelVisuals = {};

        private Animator animator;
        private int wheelCount;
        private Quaternion[] wheelVisualRotationOffsets;
        private Vector3[] wheelVisualPositionOffsets;
        private Quaternion steeringWheelLocalRotation;
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

        [UdonSynced(UdonSyncMode.Smooth), FieldChangeCallback(nameof(AccelerationValue))] private float _accelerationValue;
        private float AccelerationValue {
            set {
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
        private float BrakeValue {
            set {
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
        private float SteeringValue {
            set {
                _steeringValue = value;

                if (animator != null && IsOperating)
                {
                    animator.SetFloat(steeringParameter, value * 0.5f + 0.5f);
                }

                if (LocalIsDriver)
                {
                    foreach (var wheel in steeredWheels) wheel.steerAngle = value * maxSteeringAngle/ steeredWheels.Length;
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

        private bool BackGear {
            set => Gear = GEAR_BACK;
            get => Gear == GEAR_BACK;
        }

        private void Start()
        {
            animator = GetComponentInParent<Animator>();

            wheelCount = wheels.Length;
            wheelVisualRotationOffsets = new Quaternion[wheelCount];
            wheelVisualPositionOffsets = new Vector3[wheelCount];

            for (var i = 0; i < wheelCount; i++)
            {
                var wheel = wheels[i];
                var wheelTransform = wheel.transform;
                var visual = wheelVisuals[i];
                if (visual == null) continue;

                wheelVisualPositionOffsets[i] = wheelTransform.InverseTransformPoint(visual.position) + Vector3.up * wheel.suspensionDistance * wheel.suspensionSpring.targetPosition;
                wheelVisualRotationOffsets[i] = Quaternion.Inverse(wheelTransform.rotation) * visual.rotation;
            }

            _isOperating = false;

            if (engineSound != null)
            {
                engineSound.playOnAwake = true;
                engineSound.loop = true;
            }
        }

        private void Update()
        {
            if (LocalIsDriver) DriverUpdate();
            LocalUpdate();
        }

        private void DriverUpdate()
        {
            var accelerationInput = Input.GetKey(backAccelerationKey) ? -1.0f : (Input.GetKey(accelerationKey) ? 1.0f : (Input.GetAxis(accelerationAxis) * (BackGear ? -1.0f : 1.0f)));
            var steeringInput = Input.GetKey(steeringKeyLeft) ? -1.0f : (Input.GetKey(steeringKeyRight) ? 1.0f : Input.GetAxis(steeringAxis));
            var brakeInput = Input.GetKey(brakeKey) ? 1.0f : Input.GetAxis(brakeAxis);

            var backGearInput = Input.GetAxis(backGearAxis);
            if (Mathf.Abs(backGearInput) > 0.5f) BackGear = backGearInput < -0.5f;

            var deltaTime = Time.deltaTime;

            AccelerationValue = LinearLerp(AccelerationValue, accelerationInput, accelerationResponse * deltaTime, -1.0f, 1.0f);
            BrakeValue = (brakeInput < 0.1f) ? 0.0f : LinearLerp(BrakeValue, brakeInput, brakeResponse * deltaTime, 0.0f, 1.0f);
            SteeringValue = LinearLerp(SteeringValue, steeringInput, steeringResponse * deltaTime, -1.0f, 1.0f);

            foreach (var wheel in steeredWheels) wheel.steerAngle = SteeringValue * maxSteeringAngle;
            foreach (var wheel in drivingWheels) wheel.motorTorque = AccelerationValue * accelerationTorque / drivingWheels.Length;
            foreach (var wheel in brakeWheels) wheel.brakeTorque = BrakeValue * brakeTorque;
        }

        private void LocalUpdate()
        {
            if (!IsOperating) return;

            for (var i = 0; i < wheelCount; i++)
            {
                var visual = wheelVisuals[i];
                if (visual == null) continue;

                var wheel = wheels[i];
                var wheelTransform = wheel.transform;

                Vector3 position;
                Quaternion rotation;
                wheel.GetWorldPose(out position, out rotation);
                visual.position = wheelTransform.TransformPoint(wheelTransform.InverseTransformPoint(position) + wheelVisualPositionOffsets[i]);
                visual.rotation = rotation * wheelVisualRotationOffsets[i];
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
        }

        private float LinearLerp(float currentValue, float targetValue, float speed, float minValue, float maxValue)
        {
            return Mathf.Clamp(Mathf.Lerp(currentValue, targetValue, speed), minValue, maxValue);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void Reset()
        {
            engineSoundPitch = new AnimationCurve(new[] {
                new Keyframe(0.0f, 1.0f),
                new Keyframe(1.0f, 1.2f),
            });
            engineSoundVolume = new AnimationCurve(new[] {
                new Keyframe(0.0f, 0.8f),
                new Keyframe(1.0f, 1.0f),
            });
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
