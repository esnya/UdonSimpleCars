using UnityEngine;

#if UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

namespace UdonSimpleCars
{
    public class USC_DetachedObjects : MonoBehaviour
    {
#if UNITY_EDITOR
        private void Reset()
        {
            hideFlags = HideFlags.DontSaveInBuild;
        }

        private void OnValidate()
        {
            var car = this.GetCar();

            if (car != null && car.detachedObjects != transform)
            {
                car.detachedObjects = transform;
                car.ApplyProxyModificationsAndSetDirty();
            }
        }
#endif
    }
}
