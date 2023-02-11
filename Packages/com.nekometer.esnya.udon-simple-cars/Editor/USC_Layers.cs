using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UdonSimpleCars
{
    public class USC_Layers
    {
        public static readonly int[] addonLayers = {
            29,
        };
        public static readonly string[] addonLayerNames = {
            "BoardingCollider"
        };
        public static readonly LayerMask[] addonLayerCollisions = {
            0b0101_1111_1111_1101_1010_1111_1101_1111,
        };

        [MenuItem("USC/Setup Layers")]
        public static void SetupAddonLayers()
        {
            var zippedAddonLayers = addonLayers
                .Zip(addonLayerNames, (layer, name) => (layer, name))
                .Zip(addonLayerCollisions, (t, collision) => (t.layer, t.name, collision));
            foreach (var (layer, name, collision) in zippedAddonLayers)
            {
                USC_EditorUtilities.SetLayerName(layer, name);
                for (var i = 0; i < 32; i++)
                {
                    Physics.IgnoreLayerCollision(layer, i, ((1 << i) & collision) == 0);
                }
            }
        }
    }
}
