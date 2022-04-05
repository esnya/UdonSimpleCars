
using UdonSharp;
using UnityEngine;
using VRC.Udon.Common.Enums;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class USC_BoardingCollider : UdonSharpBehaviour
    {
        public float updateInterval = 0.5f;

        private Quaternion localRotation;
        private Transform parent;
        private Vector3 localPosition;

        private void OnEnable()
        {
            SendCustomEventDelayedSeconds(nameof(_LateThinUpdate), Random.Range(Time.fixedUnscaledDeltaTime, updateInterval), EventTiming.LateUpdate);
        }

        private void Start()
        {
            parent = transform.parent;
            localPosition = parent.InverseTransformPoint(transform.position);
            localRotation = Quaternion.Inverse(parent.rotation) * transform.rotation;
            transform.SetParent(parent.parent, true);

            gameObject.name = $"{parent.gameObject.name}_{gameObject.name}";
        }

        public void _LateThinUpdate()
        {
            if (!gameObject.activeSelf) return;

            transform.position = parent.TransformPoint(localPosition);
            transform.rotation = parent.rotation * localRotation;

            SendCustomEventDelayedSeconds(nameof(_LateThinUpdate), updateInterval, EventTiming.LateUpdate);
        }
    }
}
