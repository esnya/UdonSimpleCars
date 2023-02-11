
using UdonSharp;
using UnityEngine;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SpeedIndicator : UdonSharpBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float maxSpeed = 50;
        public float maxAngle = 275;
        public float smoothing = 0.5f;
        private Transform vehicleTransform;
        private Vector3 prevPosition;
        private Quaternion initialRotation;
        private float speed;

        private void OnEnable()
        {
            if (vehicleTransform) prevPosition = vehicleTransform.position;
            speed = 0;
        }
        private void Start()
        {
            var vehicleRigidbody = GetComponentInParent<Rigidbody>();
            vehicleTransform = vehicleRigidbody.transform;
            prevPosition = vehicleTransform.position;

            initialRotation = transform.localRotation;
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            var position = vehicleTransform.position;
            var velocity = (position - prevPosition) * (1.0f / deltaTime);
            prevPosition = position;

            speed = Mathf.Lerp(speed, Vector3.Dot(velocity, vehicleTransform.forward), deltaTime / smoothing);
            var angle = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed) * maxAngle;

            transform.localRotation = initialRotation * Quaternion.AngleAxis(angle, axis);
        }
    }
}
