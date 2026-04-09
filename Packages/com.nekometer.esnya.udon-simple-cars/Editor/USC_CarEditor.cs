using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace UdonSimpleCars
{
    [CustomEditor(typeof(USC_Car))]
    public class USC_CarEditor : Editor
    {
        private class WheelDescriptor
        {
            public WheelCollider wheelCollider;
            public bool ignored;
            public bool steered;
            public bool driving;
            public bool brake;
            public bool detached;
            public Transform visual;

            public static IEnumerable<WheelDescriptor> GetDescriptors(SerializedObject serializedCarObject)
            {
                var car = serializedCarObject.targetObject as Component;

                var wheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.wheels));
                var detachedWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.detachedWheels));
                var steeredWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.steeredWheels));
                var drivingWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.drivingWheels));
                var brakeWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.brakeWheels));
                var wheelVisualsProperty = serializedCarObject.FindProperty(nameof(USC_Car.wheelVisuals));

                var wheels = USC_EditorUtilities.EnumerateObjectArrayProperty<WheelCollider>(wheelsProperty).ToList();
                var detachedWheels = USC_EditorUtilities.EnumerateObjectArrayProperty<WheelCollider>(detachedWheelsProperty);
                var steeredWheels = USC_EditorUtilities.EnumerateObjectArrayProperty<WheelCollider>(steeredWheelsProperty);
                var drivingWheels = USC_EditorUtilities.EnumerateObjectArrayProperty<WheelCollider>(drivingWheelsProperty);
                var brakeWheels = USC_EditorUtilities.EnumerateObjectArrayProperty<WheelCollider>(brakeWheelsProperty);
                var wheelVisuals = USC_EditorUtilities.EnumerateObjectArrayProperty<Transform>(wheelVisualsProperty).ToArray();

                return wheels.Concat(detachedWheels).Where(w => w != null).Concat(car.GetComponentsInChildren<WheelCollider>(true)).Distinct().Select(wheelCollider =>
                {
                    var index = wheels.IndexOf(wheelCollider);
                    return new WheelDescriptor()
                    {
                        wheelCollider = wheelCollider,
                        ignored = index < 0,
                        steered = steeredWheels.Contains(wheelCollider),
                        driving = drivingWheels.Contains(wheelCollider),
                        brake = brakeWheels.Contains(wheelCollider),
                        detached = detachedWheels.Contains(wheelCollider),
                        visual = index < 0 || index >= wheelVisualsProperty.arraySize ? null : wheelVisuals[index],
                    };
                });
            }

            public void OnGUI()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(wheelCollider, typeof(WheelCollider), true);
                    ignored = EditorGUILayout.ToggleLeft("Ignored", ignored, GUILayout.Width(60));

                    using (new EditorGUI.DisabledScope(ignored))
                    {
                        steered = EditorGUILayout.ToggleLeft("Steered", steered && !ignored, GUILayout.Width(60));
                        driving = EditorGUILayout.ToggleLeft("Driving", driving && !ignored, GUILayout.Width(60));
                        brake = EditorGUILayout.ToggleLeft("Brake", brake && !ignored, GUILayout.Width(60));
                        detached = EditorGUILayout.ToggleLeft("Detached", detached && !ignored, GUILayout.Width(60));
                        visual = EditorGUILayout.ObjectField(ignored ? null : visual, typeof(Transform), true) as Transform;
                    }
                }
            }

            public static void Apply(SerializedObject serializedCarObject, IEnumerable<WheelDescriptor> descriptors)
            {
                var filtered = descriptors.Where(d => !d.ignored).ToArray();
                var mainWheels = filtered.Where(d => !d.detached).ToArray();

                USC_EditorUtilities.AssignObjectArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.wheels)), mainWheels.Select(d => d.wheelCollider));
                USC_EditorUtilities.AssignObjectArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.wheelVisuals)), mainWheels.Select(d => d.visual));
                USC_EditorUtilities.AssignObjectArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.steeredWheels)), mainWheels.Where(d => d.steered).Select(d => d.wheelCollider));
                USC_EditorUtilities.AssignObjectArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.drivingWheels)), mainWheels.Where(d => d.driving).Select(d => d.wheelCollider));
                USC_EditorUtilities.AssignObjectArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.brakeWheels)), mainWheels.Where(d => d.brake).Select(d => d.wheelCollider));
                USC_EditorUtilities.AssignObjectArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.detachedWheels)), filtered.Where(d => d.detached).Select(d => d.wheelCollider));
            }
        }

        private void OnWheelsGUI()
        {
            var descriptors = WheelDescriptor.GetDescriptors(serializedObject).ToArray();
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Wheels", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("", GUILayout.Width(60 * 5));
                    EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
                }
                foreach (var descriptor in descriptors)
                {
                    descriptor.OnGUI();
                }
                if (change.changed) WheelDescriptor.Apply(serializedObject, descriptors);
            }
        }

        private static readonly Dictionary<string, string> gameObjectNameTable = new Dictionary<string, string>() {
            { nameof(USC_Car.engineSound), "EngineSound" },
            { nameof(USC_Car.steeringWheel), "SteeringWheel" },
            { nameof(USC_Car.operatingOnly), "OperatingOnly" },
            { nameof(USC_Car.inVehicleOnly), "InVehicleOnly" },
            { nameof(USC_Car.driverOnly), "DriverOnly" },
            { nameof(USC_Car.backGearOnly), "BackGearOnlny" },
            { nameof(USC_Car.brakingOnly), "BrakingOnly" },
            { nameof(USC_Car.detachedObjects), "DetachedObjects" },
        };

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            foreach (var property in USC_EditorUtilities.GetVisibleProperties(serializedObject))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (property.name == nameof(USC_Car.steeringAxis)
                        || property.name == nameof(USC_Car.accelerationAxis)
                        || property.name == nameof(USC_Car.brakeAxis)
                        || property.name == nameof(USC_Car.backGearAxis))
                    {
                        USC_EditorUtilities.StringPopupField(property, USC_EditorUtilities.CommonVrAxes);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }

                    if (gameObjectNameTable.ContainsKey(property.name))
                    {
                        var name = gameObjectNameTable[property.name];
                        USC_EditorUtilities.DrawFindByNameButton(serializedObject, property, name, typeof(USC_Car));
                    }
                }
            }

            OnWheelsGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
