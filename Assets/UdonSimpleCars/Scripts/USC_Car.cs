
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

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
        public float maxSteeringAngle = 20.0f;
        [Range(0, 10)] public float steeringResponse = 1f;

        [SectionHeader("Sounds")]
        public AudioSource engineSound;
        public AnimationCurve engineSoundPitch = AnimationCurve.Linear(0, 1.0f, 1, 1.5f), engineSoundVolume = AnimationCurve.Linear(0, 0.8f, 1, 1.0f);

        [SectionHeader("VR Input")]
        [Popup("GetAxisList")] public string steeringAxis = "Oculus_CrossPlatform_SecondaryThumbstickHorizontal";
        [Popup("GetAxisList")] public string accelerationAxis = "Oculus_CrossPlatform_SecondaryIndexTrigger";
        [Popup("GetAxisList")] public string brakeAxis = "Oculus_CrossPlatform_PrimaryIndexTrigger";
        [Popup("GetAxisList")] public string backGearAxis = "Vertical";

        [SectionHeader("Keyboard Input")]
        public KeyCode steeringKeyLeft = KeyCode.A;
        public KeyCode steeringKeyRight = KeyCode.D;
        public KeyCode accelerationKey = KeyCode.LeftShift;
        public KeyCode backAccelerationKey = KeyCode.LeftControl;
        public KeyCode brakeKey = KeyCode.B;

        private new Rigidbody rigidbody;
        private int wheelCount;
        private WheelCollider[] wheels;
        private Vector2[] wheelPositions;
        private Quaternion[] wheelLocalRotations;
        private Transform[] wheelTransforms;
        private float[] wheelAngles;
        private bool localIsDriver;
        private bool backGear = false;

        [UdonSynced(UdonSyncMode.Smooth)] private float accelerationValue, brakeValue, steeringValue;
        [UdonSynced, FieldChangeCallback(nameof(IsDrived))] private bool _isDrived;
        private bool IsDrived
        {
            set
            {
                _isDrived = value;
                SetBrake(value ? 0.0f : 1.0f);
                if (engineSound != null) engineSound.gameObject.SetActive(value);
            }
            get => _isDrived;
        }

        private void Start()
        {
            rigidbody = GetComponent<Rigidbody>();
            wheels = GetComponentsInChildren<WheelCollider>();

            wheelCount = wheels.Length;
            wheelPositions = new Vector2[wheelCount];
            wheelTransforms = new Transform[wheelCount];
            wheelLocalRotations = new Quaternion[wheelCount];
            wheelAngles = new float[wheelCount];

            var worldCenterOfMass = rigidbody.worldCenterOfMass;
            var worldForward = rigidbody.transform.forward;
            var worldRight = rigidbody.transform.forward;
            for (var i = 0; i < wheelCount; i++)
            {
                var wheel = wheels[i];
                var wheelTransform = wheel.transform;
                wheelTransforms[i] = wheelTransform;

                var worldPosition = wheelTransform.TransformPoint(wheel.center);
                var relativePosition = worldPosition - worldCenterOfMass;
                wheelPositions[i] = new Vector2(
                    Vector3.Dot(relativePosition, worldRight),
                    Vector3.Dot(relativePosition, worldForward)
                );

                wheelLocalRotations[i] = wheel.transform.localRotation;
            }

            _isDrived = false;

            if (engineSound != null)
            {
                engineSound.playOnAwake = true;
                engineSound.loop = true;
            }
        }

        private void Update()
        {
            if (localIsDriver) DriverUpdate();
            if (IsDrived) DrivedUpdate();
        }

        private void DriverUpdate()
        {
            var accelerationInput = Input.GetKey(backAccelerationKey) ? -1.0f : (Input.GetKey(accelerationKey) ? 1.0f : (Input.GetAxis(accelerationAxis) * (backGear ? -1.0f : 1.0f)));
            var steeringInput = Input.GetKey(steeringKeyLeft) ? -1.0f : (Input.GetKey(steeringKeyRight) ? 1.0f : Input.GetAxis(steeringAxis));
            var brakeInput = Input.GetKey(brakeKey) ? 1.0f : Input.GetAxis(brakeAxis);

            var backGearInput = Input.GetAxis(backGearAxis);
            if (Mathf.Abs(backGearInput) > 0.5f) backGear = backGearInput < -0.5f;

            var deltaTime = Time.deltaTime;

            accelerationValue = LinearLerp(accelerationValue, accelerationInput, accelerationResponse * deltaTime, -1.0f, 1.0f);
            brakeValue = (brakeInput < 0.1f) ? 0.0f : LinearLerp(brakeValue, brakeInput, brakeResponse * deltaTime, 0.0f, 1.0f);
            steeringValue = LinearLerp(steeringValue, steeringInput, steeringResponse * deltaTime, -1.0f, 1.0f);

            for (var i = 0; i < wheelCount; i++)
            {
                var wheel = wheels[i];
                var wheelPosition = wheelPositions[i];

                wheel.motorTorque = accelerationValue * accelerationTorque / wheelCount;

                if (wheelPosition.y > 0)
                {
                    wheel.steerAngle = steeringValue * maxSteeringAngle;
                }
            }

            SetBrake(brakeValue);
        }

        private void DrivedUpdate()
        {

            if (engineSound != null)
            {
                engineSound.pitch = engineSoundPitch.Evaluate(accelerationValue);
                engineSound.volume = engineSoundVolume.Evaluate(accelerationValue);
            }

            for (var i = 0; i < wheelCount; i++)
            {
                var wheelPosition = wheelPositions[i];
                var wheelTransform = wheelTransforms[i];
                var wheelLocalRotation = wheelLocalRotations[i];
                var wheel = wheels[i];

                wheelAngles[i] = (wheelAngles[i] + wheel.rpm / (Time.deltaTime * 60.0f )) % 360.0f;
                var wheelRotation = Quaternion.AngleAxis(wheelAngles[i], Vector3.right);
                wheelTransform.localRotation = (wheelPosition.y > 0 ? (Quaternion.AngleAxis(steeringValue * maxSteeringAngle, Vector3.up) * wheelRotation) : wheelRotation) * wheelLocalRotation;
            }
        }

        public void _GetIn()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            localIsDriver = true;

            backGear = false;
            IsDrived = true;
        }

        public void _GetOut()
        {
            localIsDriver = false;
            IsDrived = false;
        }

        private void SetBrake(float value)
        {
            for (var i = 0; i < wheelCount; i++)
            {
                wheels[i].brakeTorque = value * brakeTorque / wheelCount;
            }
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
#endif
    }
}
