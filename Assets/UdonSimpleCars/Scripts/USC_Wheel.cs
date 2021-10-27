
using UnityEngine;
using UdonSharpEditor;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

namespace UdonSimpleCars
{
    [RequireComponent(typeof(WheelCollider))]
    public class USC_Wheel : MonoBehaviour
    {
        private void Reset()
        {
            hideFlags = HideFlags.DontSaveInBuild;
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(USC_Wheel))]
    public class USC_WheelEditor : Editor
    {
        private static void WheelCapabilityField(ref WheelCollider[] list, string label, WheelCollider wheelCollider)
        {
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                var value = EditorGUILayout.Toggle(label, list.Contains(wheelCollider));
                if (!change.changed) return;

                if (value) list = list.Append(wheelCollider).Where(w => w != null).Distinct().ToArray();
                else list = list.Where(w => w != wheelCollider).Where(w => w != null).Distinct().ToArray();
            }
        }

        private static void WheelObjectField<T>(ref T[] list, string label, WheelCollider[] wheels, WheelCollider wheelCollider, bool allowSceneObjects) where T : UnityEngine.Object
        {
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                var value = EditorGUILayout.ObjectField(label, list.Zip(wheels, (v, w) => w == wheelCollider ? v : null).FirstOrDefault(w => w != null), typeof(T), allowSceneObjects) as T;
                if (!change.changed) return;

                list = wheels.Zip(list.Concat(Enumerable.Repeat(null as T, wheels.Length)), (w, v) => w == wheelCollider ? value : v).ToArray();
            }
        }

        public override void OnInspectorGUI()
        {
            var wheel = target as USC_Wheel;
            var car = wheel.GetUdonSharpComponentInParent<USC_Car>();
            if (car == null)
            {
                EditorGUILayout.HelpBox("USC_Wheel(s) must be child of a USC_Car", MessageType.Error);
                return;
            }

            car.wheels = car.GetComponentsInChildren<USC_Wheel>().Select(w => w.GetComponent<WheelCollider>()).ToArray();

            var wheelCollider = wheel.GetComponent<WheelCollider>();

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                WheelCapabilityField(ref car.steeredWheels, "Steering", wheelCollider);
                WheelCapabilityField(ref car.drivingWheels, "Drive", wheelCollider);
                WheelCapabilityField(ref car.brakeWheels, "Brake", wheelCollider);
                // WheelCapabilityField(ref car.parkingBrakeWheels, "Parking Brake", wheelCollider);

                WheelObjectField(ref car.wheelVisuals, "Visual Transform", car.wheels, wheelCollider, true);

                if (change.changed)
                {
                    car.steeringWheelTransforms = car.steeredWheels.Select(sw => car.wheels.Zip(car.wheelVisuals, (w, t) => w == sw ? t : null).FirstOrDefault(t => t != null)).ToArray();
                    car.ApplyProxyModifications();
                    EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(car));
                }
            }
        }
    }
#endif
}
