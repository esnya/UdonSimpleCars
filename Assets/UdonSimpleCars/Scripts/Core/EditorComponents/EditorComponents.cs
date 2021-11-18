#if UNITY_EDITOR
using UnityEngine;
using UdonSharpEditor;
using UnityEditor;

namespace UdonSimpleCars
{
    public static class EditorComponentUtilites
    {
        public static USC_Car GetCar(this Component target)
        {
            var car = target.GetUdonSharpComponentInParent<USC_Car>();
            if (car == null) EditorGUILayout.HelpBox($"{target.GetType().Name}(s) must be child of a USC_Car", MessageType.Error);
            return car;
        }

        public static void ApplyProxyModificationsAndSetDirty(this USC_Car car)
        {
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(car);
            Undo.RecordObject(udon, "Edit Public Variables");
            car.ApplyProxyModifications();
            EditorUtility.SetDirty(udon);
        }
    }

    public class CarEditScope : EditorGUI.ChangeCheckScope
    {
        private readonly USC_Car car;
        public CarEditScope(USC_Car car)
        {
            this.car = car;
        }

        protected override void CloseScope()
        {
            car.ApplyProxyModificationsAndSetDirty();
            base.CloseScope();
        }
    }
}
#endif
