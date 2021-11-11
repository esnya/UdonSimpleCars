using UdonSharp;
using UnityEngine;

namespace UdonSimpleCars
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class USC_Respawner : UdonSharpBehaviour
    {
        public USC_Car target;

        public override void Interact()
        {
            target._Respawn();
        }
    }
}
