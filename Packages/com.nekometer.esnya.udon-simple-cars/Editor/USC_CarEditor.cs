using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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

            private static IEnumerable<T> EnumrateArrayProperty<T>(SerializedProperty arrayProperty) where T : Object
            {
                return Enumerable.Range(0, arrayProperty.arraySize).Select(i => arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue as T);
            }

            private static void AssignArrayProperty<T>(SerializedProperty arrayProperty, IEnumerable<T> values) where T : Object
            {
                var valueArray = values.ToArray();
                arrayProperty.arraySize = valueArray.Length;

                foreach (var i in Enumerable.Range(0, valueArray.Length))
                {
                    arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = valueArray[i];
                }
            }

            public static IEnumerable<WheelDescriptor> GetDescriptors(SerializedObject serializedCarObject)
            {
                var car = serializedCarObject.targetObject as Component;

                var wheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.wheels));
                var detachedWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.detachedWheels));
                var steeredWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.steeredWheels));
                var drivingWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.drivingWheels));
                var brakeWheelsProperty = serializedCarObject.FindProperty(nameof(USC_Car.brakeWheels));
                var wheelVisualsProperty = serializedCarObject.FindProperty(nameof(USC_Car.wheelVisuals));

                var wheels = EnumrateArrayProperty<WheelCollider>(wheelsProperty).ToList();
                var detachedWheels = EnumrateArrayProperty<WheelCollider>(detachedWheelsProperty);
                var steeredWheels = EnumrateArrayProperty<WheelCollider>(steeredWheelsProperty);
                var drivingWheels = EnumrateArrayProperty<WheelCollider>(drivingWheelsProperty);
                var brakeWheels = EnumrateArrayProperty<WheelCollider>(brakeWheelsProperty);
                var wheelVisuals = EnumrateArrayProperty<Transform>(wheelVisualsProperty).ToArray();

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

                AssignArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.wheels)), mainWheels.Select(d => d.wheelCollider));
                AssignArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.wheelVisuals)), mainWheels.Select(d => d.visual));
                AssignArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.steeredWheels)), mainWheels.Where(d => d.steered).Select(d => d.wheelCollider));
                AssignArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.drivingWheels)), mainWheels.Where(d => d.driving).Select(d => d.wheelCollider));
                AssignArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.brakeWheels)), mainWheels.Where(d => d.brake).Select(d => d.wheelCollider));
                AssignArrayProperty(serializedCarObject.FindProperty(nameof(USC_Car.detachedWheels)), filtered.Where(d => d.detached).Select(d => d.wheelCollider));
            }
        }

        private void OnWheelsGUI(SerializedProperty property)
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

            while (property.name.ToLower().Contains("wheel")) property.NextVisible(false);
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

            var property = serializedObject.GetIterator();
            property.NextVisible(true);

            while (property.NextVisible(false))
            {
                if (property.name == nameof(USC_Car.wheels)) OnWheelsGUI(property);
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(property, true);

                        if (gameObjectNameTable.ContainsKey(property.name))
                        {
                            var name = gameObjectNameTable[property.name];
                            if (GUILayout.Button("Find", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                            {
                                var gameObject = EditorUtility.CollectDeepHierarchy(serializedObject.targetObjects)
                                    .Select(o => o as GameObject)
                                    .FirstOrDefault(o => o && o.name == name);

                                if (gameObject)
                                {
                                    var fieldType = typeof(USC_Car).GetField(property.name, BindingFlags.Instance | BindingFlags.DeclaredOnly)?.FieldType;
                                    property.objectReferenceValue = fieldType?.IsSubclassOf(typeof(Component)) == true ? (UnityEngine.Object)gameObject.GetComponent(fieldType) : gameObject;
                                }
                            }
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
