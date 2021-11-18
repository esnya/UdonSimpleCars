
using UnityEngine;
#if UNITY_EDITOR
using UdonSharpEditor;
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

#if UNITY_EDITOR
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
            var target = this.target as USC_Wheel;
            var car = EditorComponentUtilites.GetCar(target);
            if (car == null) return;

            var wheels = car.GetComponentsInChildren<USC_Wheel>().Select(w => w.GetComponent<WheelCollider>()).ToArray();
            var wheelCollider = target.GetComponent<WheelCollider>();

            using (var change = new CarEditScope(car))
            {
                WheelCapabilityField(ref car.steeredWheels, "Steering", wheelCollider);
                WheelCapabilityField(ref car.drivingWheels, "Drive", wheelCollider);
                WheelCapabilityField(ref car.brakeWheels, "Brake", wheelCollider);

                WheelObjectField(ref car.wheelVisuals, "Visual Transform", wheels, wheelCollider, true);

                if (change.changed)
                {
                    car.wheels = wheels;
                }
            }
        }
    }
#endif
}
