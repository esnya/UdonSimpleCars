using UdonSharpEditor;
using UnityEditor;

namespace UdonSimpleCars
{
    [CustomEditor(typeof(USC_Seat))]
    public class USC_SeatEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            foreach (var property in USC_EditorUtilities.GetVisibleProperties(serializedObject))
            {
                if (property.name == nameof(USC_Seat.getOutButton))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);

                if (property.name == nameof(USC_Seat.getOutByJump) && !property.boolValue)
                {
                    var getOutButton = serializedObject.FindProperty(nameof(USC_Seat.getOutButton));
                    USC_EditorUtilities.StringPopupField(getOutButton, USC_EditorUtilities.CommonVrButtons);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
