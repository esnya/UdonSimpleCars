using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace UdonSimpleCars
{
    public static class USC_EditorUtilities
    {
        public static readonly string[] CommonVrAxes =
        {
            "Oculus_CrossPlatform_PrimaryIndexTrigger",
            "Oculus_CrossPlatform_SecondaryIndexTrigger",
            "Oculus_CrossPlatform_PrimaryHandTrigger",
            "Oculus_CrossPlatform_SecondaryHandTrigger",
            "Horizontal",
            "Oculus_CrossPlatform_SecondaryThumbstickHorizontal",
            "Vertical",
            "Oculus_CrossPlatform_SecondaryThumbstickVertical",
        };

        public static readonly string[] CommonVrButtons =
        {
            "Oculus_CrossPlatform_PrimaryThumbstick",
            "Oculus_CrossPlatform_SecondaryThumbstick",
            "Oculus_CrossPlatform_Button4",
            "Oculus_CrossPlatform_Button2",
        };

        public static void SetLayerName(int layer, string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset"));
            tagManager.Update();

            var layersProperty = tagManager.FindProperty("layers");
            layersProperty.arraySize = Mathf.Max(layersProperty.arraySize, layer);
            layersProperty.GetArrayElementAtIndex(layer).stringValue = name;

            tagManager.ApplyModifiedProperties();
        }

        public static IEnumerable<SerializedProperty> GetVisibleProperties(SerializedObject serializedObject)
        {
            var property = serializedObject.GetIterator();
            if (!property.NextVisible(true))
            {
                yield break;
            }

            while (property.NextVisible(false))
            {
                yield return property.Copy();
            }
        }

        public static IEnumerable<T> EnumerateObjectArrayProperty<T>(SerializedProperty arrayProperty) where T : Object
        {
            return Enumerable.Range(0, arrayProperty.arraySize)
                .Select(i => arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue as T);
        }

        public static void AssignObjectArrayProperty<T>(SerializedProperty arrayProperty, IEnumerable<T> values) where T : Object
        {
            var valueArray = values.ToArray();
            arrayProperty.arraySize = valueArray.Length;

            for (var i = 0; i < valueArray.Length; i++)
            {
                arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = valueArray[i];
            }
        }

        public static void ResizeArrays(int size, params SerializedProperty[] arrays)
        {
            foreach (var array in arrays)
            {
                array.arraySize = size;
            }
        }

        public static void StringPopupField(SerializedProperty property, IEnumerable<string> values, GUIContent label = null, string emptyLabel = null)
        {
            property.stringValue = StringPopupField(label ?? new GUIContent(property.displayName), property.stringValue, values, emptyLabel);
        }

        public static string StringPopupField(GUIContent label, string value, IEnumerable<string> values, string emptyLabel = null)
        {
            var options = values?.Where(v => !string.IsNullOrEmpty(v)).Distinct().ToList() ?? new List<string>();
            if (options.Count == 0)
            {
                return EditorGUILayout.TextField(label, value);
            }

            if (emptyLabel != null)
            {
                options.Insert(0, emptyLabel);
            }

            var currentValue = string.IsNullOrEmpty(value) && emptyLabel != null ? emptyLabel : value;
            var index = Mathf.Max(options.FindIndex(v => v == currentValue), 0);
            index = EditorGUILayout.Popup(label, index, options.ToArray());

            var newValue = options[index];
            return newValue == emptyLabel ? null : newValue;
        }

        public static void UdonPublicEventField(SerializedProperty udonProperty, SerializedProperty valueProperty, GUIContent label)
        {
            valueProperty.stringValue = UdonPublicEventField(label, udonProperty.objectReferenceValue as UdonSharpBehaviour, valueProperty.stringValue);
        }

        public static string UdonPublicEventField(GUIContent label, UdonSharpBehaviour udon, string value)
        {
            if (udon == null) return EditorGUILayout.TextField(label, value);

            var events = udon.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && method.ReturnType == typeof(void) && method.GetParameters().Length == 0)
                .Select(method => method.Name)
                .Distinct()
                .OrderBy(name => name)
                .Prepend("(null)")
                .ToArray();

            if (events.Length <= 1) return EditorGUILayout.TextField(label, value);

            var index = Mathf.Max(Array.IndexOf(events, value), 0);
            index = EditorGUILayout.Popup(label, index, events);

            var newValue = events[index];
            return newValue == "(null)" ? null : newValue;
        }

        public static bool DrawFindByNameButton(SerializedObject serializedObject, SerializedProperty property, string gameObjectName, Type componentOwnerType)
        {
            if (!GUILayout.Button("Find", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
            {
                return false;
            }

            var gameObject = EditorUtility.CollectDeepHierarchy(serializedObject.targetObjects)
                .OfType<GameObject>()
                .FirstOrDefault(o => o != null && o.name == gameObjectName);

            if (!gameObject)
            {
                return false;
            }

            var fieldType = componentOwnerType
                .GetField(property.name, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic)
                ?.FieldType;

            property.objectReferenceValue = fieldType?.IsSubclassOf(typeof(Component)) == true
                ? gameObject.GetComponent(fieldType)
                : gameObject;
            return true;
        }
    }
}
