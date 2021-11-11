using UnityEngine;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

namespace UdonSimpleCars
{
    [RequireComponent(typeof(AudioSource))]
    public class USC_EngineSound : MonoBehaviour
    {
        private void Reset()
        {
            hideFlags = HideFlags.DontSaveInBuild;
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(USC_EngineSound))]
    public class USC_EngineSoundEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var target = this.target as USC_EngineSound;

            var car = EditorComponentUtilites.GetCar(target);
            if (car == null) return;

            using (var scope = new CarEditScope(car))
            {
                car.engineSoundVolume = EditorGUILayout.CurveField("Volume", car.engineSoundVolume);
                car.engineSoundPitch = EditorGUILayout.CurveField("Pitch", car.engineSoundPitch);

                if (scope.changed)
                {
                    car.engineSound = target.GetComponent<AudioSource>();
                }
            }
        }
    }
#endif
}
