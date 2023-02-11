
using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace UdonSimpleCars
{
    [RequireComponent(typeof(VRCStation))]
    [RequireComponent(typeof(Collider))]
    [DefaultExecutionOrder(1000)] // After USC_Car
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class USC_Seat : UdonSharpBehaviour
    {
        public bool getOutByJump = true;
        [HideIf("@getOutByJump")][Popup("GetButtonList")] public string getOutButton = "Oculus_CrossPlatform_Button4";
        public KeyCode getOutKey = KeyCode.Return;
        public bool isDriver = true;
        private USC_Car car;
        private VRCStation station;
        private USC_RecoveryStation recoveryStation;

        public GameObject ThisSeatOnly;
        public bool AdjustSeat = true;
        public Transform TargetEyePosition;
        [UdonSynced, FieldChangeCallback(nameof(AdjustedPos))] private Vector2 _adjustedPos;
        public Vector2 AdjustedPos
        {
            set
            {
                _adjustedPos = value;
                SetRecievedSeatPosition();
            }
            get => _adjustedPos;
        }
        private float AdjustTime;
        private bool CalibratedY = false;
        private bool CalibratedZ = false;
        private Vector3 SeatStartPos;
        private Transform Seat;
        private bool InSeat;
        private bool InEditor = true;
        private VRCPlayerApi localPlayer;
        private Quaternion SeatStartRot;
        private void Start()
        {
            car = GetComponentInParent<USC_Car>();
            station = (VRCStation)GetComponent(typeof(VRCStation));
            recoveryStation = GetComponentInChildren<USC_RecoveryStation>(true);


            localPlayer = Networking.LocalPlayer;
            if (localPlayer != null) { InEditor = false; }
            Seat = ((VRC.SDK3.Components.VRCStation)GetComponent(typeof(VRC.SDK3.Components.VRCStation))).stationEnterPlayerLocation.transform;
            SeatStartRot = Seat.localRotation;
            SeatStartPos = Seat.localPosition;
            if (InEditor && ThisSeatOnly) { ThisSeatOnly.SetActive(true); }
        }

        private void Update()
        {
            if ((Input.GetKey(getOutKey) || !getOutByJump && Input.GetButton(getOutButton)) && InSeat)
            {
                station.ExitStation(localPlayer);
            }
        }

        public override void Interact()
        {
            _Enter();
        }

        public void _Enter()
        {
            if (!localPlayer.IsOwner(obj: gameObject))
            { Networking.SetOwner(localPlayer, gameObject); }
            Seat.rotation = Quaternion.Euler(0, Seat.eulerAngles.y, 0);//fixes offset seated position when getting in a rolled/pitched vehicle in VR
            localPlayer.UseAttachedStation();
            Seat.localRotation = SeatStartRot;
        }

        public override void OnStationEntered(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                if (isDriver) car._OnEnteredAsDriver();
                else car._OnEnteredAsPassenger();
                InSeat = true;
                if (AdjustSeat && TargetEyePosition)
                {
                    CalibratedY = false;
                    CalibratedZ = false;
                    AdjustTime = 0;
                    SeatAdjustment();
                }
                if (ThisSeatOnly) ThisSeatOnly.SetActive(true);
            }
        }

        public override void OnStationExited(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                Seat.localPosition = SeatStartPos;
                InSeat = false;
                if (ThisSeatOnly) ThisSeatOnly.SetActive(true);
                car._OnExited();
                if (recoveryStation != null) recoveryStation._Enter();
            }
        }
        //seat adjuster stuff
        public void SeatAdjustment()
        {
            if (InSeat)
            {
                if (!InEditor)
                {
                    AdjustTime += .3f;
                    //find head relative position ingame
                    Vector3 TargetRelative = TargetEyePosition.InverseTransformPoint(localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position);
                    if (!CalibratedY)
                    {
                        if (Mathf.Abs(TargetRelative.y) > 0.01f)
                        {
                            Seat.position -= TargetEyePosition.up * FindNearestPowerOf2Below(TargetRelative.y);
                        }
                        else
                        {
                            if (AdjustTime > 1f)
                            {
                                CalibratedY = true;
                            }
                        }
                    }
                    if (!CalibratedZ)
                    {
                        if (Mathf.Abs(TargetRelative.z) > 0.01f)
                        {
                            Seat.position -= TargetEyePosition.forward * FindNearestPowerOf2Below(TargetRelative.z);
                        }
                        else
                        {
                            if (AdjustTime > 1f)
                            {
                                CalibratedZ = true;
                            }
                        }
                    }
                    //remove floating point errors on x
                    Vector3 seatpos = Seat.localPosition;
                    seatpos.x = 0;
                    Seat.localPosition = seatpos;
                    //set synced variable
                    Vector3 newpos = Seat.localPosition;
                    _adjustedPos.x = newpos.y;
                    _adjustedPos.y = newpos.z;
                    RequestSerialization();
                    if ((!CalibratedY || !CalibratedZ))
                    {
                        SendCustomEventDelayedSeconds(nameof(SeatAdjustment), .3f, VRC.Udon.Common.Enums.EventTiming.LateUpdate);
                    }
                }
            }
        }
        private float FindNearestPowerOf2Below(float target)
        {
            float targetAbs = Mathf.Abs(target);
            float x = .01f;
            while (x < targetAbs)
            { x *= 2; }
            x *= .5f;
            if (target > 0)
            { return x; }
            else
            { return -x; }
        }
        public void SetRecievedSeatPosition()
        {
            if (Seat)
            {
                Vector3 newpos = (new Vector3(0, _adjustedPos.x, _adjustedPos.y));
                Seat.localPosition = newpos;
            }
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (value && getOutByJump && localPlayer.IsUserInVR())
            {
                station.ExitStation(localPlayer);
            }
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
