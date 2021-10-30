namespace UdonSimpleCars
{
    using UdonSharp;
    using UnityEngine;
    using VRC.SDKBase;

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(VRC.SDK3.Components.VRCStation))]
    public class USC_RecoveryStation : UdonSharpBehaviour
    {
        private VRCStation station, parentStation;
        private void Start()
        {
            station = (VRCStation)GetComponent(typeof(VRCStation));
            parentStation = (VRCStation)transform.parent.GetComponentInParent(typeof(VRCStation));

            station.stationEnterPlayerLocation = parentStation.stationEnterPlayerLocation;
            station.stationExitPlayerLocation = parentStation.stationExitPlayerLocation;

            gameObject.SetActive(false);
        }

        public override void OnStationEntered(VRCPlayerApi player)
        {
            if (player.isLocal) station.ExitStation(player);
        }

        public override void OnStationExited(VRCPlayerApi player)
        {
            if (player.isLocal) gameObject.SetActive(false);
        }

        public void _Enter()
        {
            gameObject.SetActive(true);
            Networking.LocalPlayer.UseAttachedStation();
        }
    }
}