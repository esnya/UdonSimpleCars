using System;
using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDKBase;

namespace UdonSimpleCars
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(Rigidbody))]
    public class USC_RigidbodyEventDispatcher : UdonSharpBehaviour
    {
        // ToDo: Enum
        public string[] GetEventTypes() => new [] {
            "OnTriggerEnter",
            "OnTriggerExit",
            "OnCollisionEnter",
            "OnCollisionExit",
            "OnPlayerTriggerEnter",
            "OnPlayerTriggerExit",
            "OnPlayerCollisionEnter",
            "OnPlayerCollisionExit",
        };
        public const int EVENT_TYPE_ON_TRIGGER_ENTER = 0;
        public const int EVENT_TYPE_ON_TRIGGER_EXIT = 1;
        public const int EVENT_TYPE_ON_COLLISION_ENTER = 2;
        public const int EVENT_TYPE_ON_COLLISION_EXIT = 3;
        public const int EVENT_TYPE_ON_PLAYER_TRIGGER_ENTER = 4;
        public const int EVENT_TYPE_ON_PLAYER_TRIGGER_EXIT = 5;
        public const int EVENT_TYPE_ON_PLAYER_COLLISION_ENTER = 6;
        public const int EVENT_TYPE_ON_PLAYER_COLLISION_EXIT = 7;

        [ListView("Event Targets")] public UdonSharpBehaviour[] eventTargets = { };
        [ListView("Event Targets")][Popup("GetEventTypes")] public int[] eventTypes = { };
        [ListView("Event Targets")][Popup("behaviour", "@eventTargets")] public string[] eventNames = { };

        // private int[] eventTypes;
        private bool[] hasEvents;

        private void Start()
        {
            // eventTypes = new int[eventTypeNames.Length];
            // for (var i = 0; i < eventTypes.Length; i++)
            // {
            //     eventTypes[i] = Array.IndexOf(GetEventTypes(), eventTypeNames[i]);
            // }

            hasEvents = new bool[GetEventTypes().Length];
            for (var i = 0; i < hasEvents.Length; i++)
            {
                hasEvents[i] = Array.IndexOf(eventTypes, i) >= 0;
            }
        }

        private void DispatchEvent(int eventType)
        {
            if (!hasEvents[eventType])
            {
                return;
            }

            int eventTargetCount = eventTargets.Length;
            for (int i = 0; i < eventTargetCount; i++)
            {
                if (eventTypes[i] != eventType)
                {
                    continue;
                }

                UdonSharpBehaviour eventTarget = eventTargets[i];
                if (!eventTarget)
                {
                    continue;
                }

                eventTarget.SendCustomEvent(eventNames[i]);
            }
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            DispatchEvent(EVENT_TYPE_ON_PLAYER_TRIGGER_ENTER);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            DispatchEvent(EVENT_TYPE_ON_PLAYER_TRIGGER_EXIT);
        }

        public override void OnPlayerCollisionEnter(VRCPlayerApi player)
        {
            DispatchEvent(EVENT_TYPE_ON_PLAYER_COLLISION_ENTER);
        }

        public override void OnPlayerCollisionExit(VRCPlayerApi player)
        {
            DispatchEvent(EVENT_TYPE_ON_PLAYER_COLLISION_EXIT);
        }
    }
}
