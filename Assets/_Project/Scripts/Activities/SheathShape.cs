using UnityEngine;

namespace Blacksmith.Activities
{
    // 칼집 단계들이 함께 쓰는 모양: 작업대에 눕힌 칼집(입구가 왼쪽, 끝이 오른쪽).
    // 로컬 좌표: x = 길이(입구 -L/2 → 끝 +L/2), z = 폭(가운데 0). 폭은 입구에서 끝으로 갈수록 좁아진다.
    public static class SheathShape
    {
        public const float Length = 0.34f;

        // u: 0 = 입구, 1 = 끝
        public static float HalfWidth(float u) => Mathf.Lerp(0.034f, 0.006f, Mathf.Clamp01(u) * Mathf.Clamp01(u) * 0.6f + Mathf.Clamp01(u) * 0.4f);
        public static float XOf(float u) => -Length / 2f + u * Length;
        public static float UOf(float x) => (x + Length / 2f) / Length;

        // 가죽 몸통을 조각 여러 개로 깔아 가늘어지는 모양을 낸다
        public static void Build(Transform root, Material leather, int slices = 16, float y = 0f, float thick = 0.008f)
        {
            for (int i = 0; i < slices; i++)
            {
                float u0 = i / (float)slices, u1 = (i + 1) / (float)slices;
                float w = HalfWidth((u0 + u1) / 2f) * 2f;
                ActivityKit.Prim(PrimitiveType.Cube, root, new Vector3((XOf(u0) + XOf(u1)) / 2f, y, 0f), new Vector3(Length / slices + 0.0005f, thick, w), leather);
            }
        }
    }
}
