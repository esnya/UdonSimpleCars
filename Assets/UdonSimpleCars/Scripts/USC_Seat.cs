
using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDKBase;

namespace UdonSimpleCars
{
    [RequireComponent(typeof(VRCStation))]
    [RequireComponent(typeof(Collider))]
    [DefaultExecutionOrder(1000)] // After USC_Car
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class USC_Seat : UdonSharpBehaviour
    {
        [Popup("GetButtonList")] public string getOutButton = "Oculus_CrossPlatform_Button4";
        public KeyCode getOutKey = KeyCode.Return;
        public bool isDriver = true;

        private USC_Car car;
        private VRCStation station;
        private void Start()
        {
            car = GetComponentInParent<USC_Car>();
            station = (VRCStation)GetComponent(typeof(VRCStation));
        }

        private void Update()
        {
            if (Input.GetKey(getOutKey) || Input.GetButton(getOutButton))
            {
                station.ExitStation(Networking.LocalPlayer);
            }
        }

        public override void Interact()
        {
            Networking.LocalPlayer.UseAttachedStation();
        }

        public override void OnStationEntered(VRCPlayerApi player)
        {
            if (player.isLocal) {
                if (isDriver) car._OnEnteredAsDriver();
                else car._OnEnteredAsPassenger();
            }
        }

        public override void OnStationExited(VRCPlayerApi player)
        {
            if (player.isLocal) car._OnExited();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private string[] GetButtonList() => new[] {
            "Oculus_CrossPlatform_PrimaryThumbstick",
            "Oculus_CrossPlatform_SecondaryThumbstick",
            "Oculus_CrossPlatform_Button4",
            "Oculus_CrossPlatform_Button2",
        };
#endif
    }
}
