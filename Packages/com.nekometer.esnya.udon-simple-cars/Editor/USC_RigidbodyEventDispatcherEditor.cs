using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace UdonSimpleCars
{
    [CustomEditor(typeof(USC_RigidbodyEventDispatcher))]
    public class USC_RigidbodyEventDispatcherEditor : Editor
    {
        private static readonly string[] SupportedEventTypeNames =
        {
            "OnPlayerTriggerEnter",
            "OnPlayerTriggerExit",
            "OnPlayerCollisionEnter",
            "OnPlayerCollisionExit",
        };

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            var eventTargets = serializedObject.FindProperty(nameof(USC_RigidbodyEventDispatcher.eventTargets));
            var eventTypes = serializedObject.FindProperty(nameof(USC_RigidbodyEventDispatcher.eventTypes));
            var eventNames = serializedObject.FindProperty(nameof(USC_RigidbodyEventDispatcher.eventNames));

            var size = Mathf.Max(eventTargets.arraySize, eventTypes.arraySize, eventNames.arraySize);
            var newSize = EditorGUILayout.IntField("Event Count", size);
            if (newSize != size)
            {
                USC_EditorUtilities.ResizeArrays(newSize, eventTargets, eventTypes, eventNames);
                size = newSize;
            }
            else if (eventTargets.arraySize != size || eventTypes.arraySize != size || eventNames.arraySize != size)
            {
                USC_EditorUtilities.ResizeArrays(size, eventTargets, eventTypes, eventNames);
            }

            for (var i = 0; i < size; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var eventTarget = eventTargets.GetArrayElementAtIndex(i);
                    var eventType = eventTypes.GetArrayElementAtIndex(i);
                    var eventName = eventNames.GetArrayElementAtIndex(i);

                    EditorGUILayout.PropertyField(eventTarget, new GUIContent($"Event {i + 1} Target"));
                    var popupIndex = Mathf.Clamp(
                        eventType.intValue - USC_RigidbodyEventDispatcher.EVENT_TYPE_ON_PLAYER_TRIGGER_ENTER,
                        0,
                        SupportedEventTypeNames.Length - 1);
                    popupIndex = EditorGUILayout.Popup(new GUIContent("Event Type"), popupIndex, SupportedEventTypeNames);
                    eventType.intValue = popupIndex + USC_RigidbodyEventDispatcher.EVENT_TYPE_ON_PLAYER_TRIGGER_ENTER;
                    USC_EditorUtilities.UdonPublicEventField(eventTarget, eventName, new GUIContent("Event Name"));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
