#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEngine;
using UdonSharpEditor;
using UnityEditor;

namespace UdonSimpleCars
{
    public static class EditorComponentUtilites
    {
        public static USC_Car GetCar(Component target)
        {
            var car = target.GetUdonSharpComponentInParent<USC_Car>();
            if (car == null) EditorGUILayout.HelpBox($"{target.GetType().Name}(s) must be child of a USC_Car", MessageType.Error);
            return car;
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
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(car);
            Undo.RecordObject(udon, "Edit Public Variables");
            car.ApplyProxyModifications();
            EditorUtility.SetDirty(udon);
            base.CloseScope();
        }
    }
}
#endif
