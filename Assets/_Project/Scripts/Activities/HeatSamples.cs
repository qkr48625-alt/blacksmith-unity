using Blacksmith.Pieces;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 작업대 옆에 세워 두는 색 견본 쇳조각(체리빛 · 검게 식음 · 산화막 색). 색만 보고 판단하는 단계에서 비교용.
    public static class HeatSamples
    {
        // 온도 견본: 그 온도로 빛나는 작은 쇳조각 + 금 테(목표)
        public static Transform Glow(Transform parent, Vector3 worldPos, Quaternion rot, float temp, bool target)
        {
            var root = ActivityKit.Root("Sample", parent, worldPos, rot);
            ActivityKit.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.06f, 0.012f, 0.04f),
                ActivityKit.Mat(new Color(0.25f, 0.24f, 0.24f), Workpiece.GlowColor(temp)));
            if (target) Frame(root, 0.07f, 0.05f);
            return root;
        }

        // 색 견본: 그 색의 쇳조각
        public static Transform Tint(Transform parent, Vector3 worldPos, Quaternion rot, Color color, bool target)
        {
            var root = ActivityKit.Root("Sample", parent, worldPos, rot);
            ActivityKit.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.05f, 0.012f, 0.04f), ActivityKit.Mat(color, color * 0.25f));
            if (target) Frame(root, 0.062f, 0.052f);
            return root;
        }

        static void Frame(Transform root, float w, float d)
        {
            var gold = ActivityKit.Mat(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.7f, 0.2f) * 2f);
            ActivityKit.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, d / 2f), new Vector3(w, 0.014f, 0.004f), gold);
            ActivityKit.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, -d / 2f), new Vector3(w, 0.014f, 0.004f), gold);
            ActivityKit.Prim(PrimitiveType.Cube, root, new Vector3(w / 2f, 0f, 0f), new Vector3(0.004f, 0.014f, d), gold);
            ActivityKit.Prim(PrimitiveType.Cube, root, new Vector3(-w / 2f, 0f, 0f), new Vector3(0.004f, 0.014f, d), gold);
        }
    }
}
