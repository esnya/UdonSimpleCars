
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class USC_CenterOfMass : UdonSharpBehaviour
    {
        private void Start()
        {
            var rigidbody = GetComponentInParent<Rigidbody>();
            if (rigidbody) rigidbody.centerOfMass = rigidbody.transform.InverseTransformPoint(transform.position);
            gameObject.SetActive(false);
        }
    }
}
