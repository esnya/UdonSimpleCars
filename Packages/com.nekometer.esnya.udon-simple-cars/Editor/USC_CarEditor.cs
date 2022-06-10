using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace UdonSimpleCars
{
    [CustomEditor(typeof(USC_Car))]
    public class USC_CarEditor : Editor
    {
        private struct WheelDescriptor
        {
            public WheelCollider wheelCollider;
            public bool ignored;
            public bool steered;
            public bool driving;
            public bool brake;
            public bool detached;
            public Transform visual;

            public static IEnumerable<WheelDescriptor> GetDescriptors(USC_Car car)
            {
                var wheels = car.wheels.ToList();
                return wheels.Concat(car.detachedWheels).Concat(car.GetComponentsInChildren<WheelCollider>(true)).Distinct().Select(wheelCollider =>
                {
                    var index = wheels.IndexOf(wheelCollider);
                    return new WheelDescriptor()
                    {
                        wheelCollider = wheelCollider,
                        ignored = index < 0,
                        steered = car.steeredWheels.Contains(wheelCollider),
                        driving = car.drivingWheels.Contains(wheelCollider),
                        brake = car.brakeWheels.Contains(wheelCollider),
                        detached = car.detachedWheels.Contains(wheelCollider),
                        visual = index < 0 || index >= (car.wheelVisuals?.Length ?? 0) ? null : car.wheelVisuals[index],
                    };
                });
            }

            public void OnGUI()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(wheelCollider, typeof(WheelCollider), true);
                    ignored = EditorGUILayout.ToggleLeft("Ignored", ignored, GUILayout.Width(60));
                    steered = EditorGUILayout.ToggleLeft("Steered", steered, GUILayout.Width(60));
                    driving = EditorGUILayout.ToggleLeft("Driving", driving, GUILayout.Width(60));
                    brake = EditorGUILayout.ToggleLeft("Brake", brake, GUILayout.Width(60));
                    detached = EditorGUILayout.ToggleLeft("Detached", detached, GUILayout.Width(60));
                    visual = EditorGUILayout.ObjectField(visual, typeof(Transform), true) as Transform;
                }
            }

            public static void Apply(USC_Car car, IEnumerable<WheelDescriptor> descriptors)
            {
                var filtered = descriptors.Where(d => !d.ignored).ToArray();

                car.wheels = filtered.Where(d => !d.detached).Select(d => d.wheelCollider).ToArray();
                car.wheelVisuals = filtered.Where(d => !d.detached).Select(d => d.visual).ToArray();

                car.steeredWheels = filtered.Where(d => d.steered).Select(d => d.wheelCollider).ToArray();
                car.drivingWheels = filtered.Where(d => d.driving).Select(d => d.wheelCollider).ToArray();
                car.brakeWheels = filtered.Where(d => d.brake).Select(d => d.wheelCollider).ToArray();
                car.detachedWheels = filtered.Where(d => d.detached).Select(d => d.wheelCollider).ToArray();
            }
        }
        private void OnWheelsGUI(SerializedProperty property)
        {

            serializedObject.ApplyModifiedProperties();

            var car = target as USC_Car;

            var descriptors = WheelDescriptor.GetDescriptors(car).ToArray();
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
                if (change.changed) WheelDescriptor.Apply(car, descriptors);
            }

            serializedObject.Update();
            while (property.name.ToLower().Contains("wheel")) property.NextVisible(false);
        }

        private static readonly Dictionary<string, string> gameObjectNameTable = new Dictionary<string, string> () {
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

            var property = serializedObject.GetIterator();
            property.NextVisible(true);

            do
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
            while (property.NextVisible(false));
        }
    }
}
