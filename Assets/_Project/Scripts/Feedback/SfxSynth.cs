using UnityEngine;

namespace Blacksmith.Feedback
{
    // 효과음을 파일 없이 코드로 만든다(웹 버전 engine/sfx.js 와 같은 생각). 나중에 녹음한 소리로 바꿀 수 있다.
    public static class SfxSynth
    {
        const int Rate = 44100;

        // 쇳소리: 서로 어긋난 배음 몇 개가 빠르게 사그라든다. bright 가 높을수록 맑다.
        public static AudioClip Clank(float bright)
        {
            float len = Mathf.Lerp(0.35f, 0.9f, bright);
            float[] partials = { 1f, 2.76f, 5.4f, 8.93f };
            return Make("clank", len, (t, rnd) =>
            {
                float v = 0f;
                for (int i = 0; i < partials.Length; i++)
                {
                    float f = 620f * partials[i];
                    float decay = Mathf.Exp(-t * (6f + i * 7f) / Mathf.Lerp(0.5f, 1.2f, bright));
                    v += Mathf.Sin(2 * Mathf.PI * f * t) * decay / (i + 1);
                }
                float hit = Mathf.Exp(-t * 90f) * (float)(rnd.NextDouble() * 2 - 1) * 0.8f;
                return (v * 0.55f + hit) * 0.8f;
            });
        }

        // 둔한 소리: 낮은 쿵 + 짧은 잡음
        public static AudioClip Thud() => Make("thud", 0.3f, (t, rnd) =>
            (Mathf.Sin(2 * Mathf.PI * 110f * t) * Mathf.Exp(-t * 18f) + (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 50f) * 0.5f) * 0.8f);

        // 풀무 바람: 낮게 걸러진 잡음이 부풀었다 빠진다
        public static AudioClip Whoosh()
        {
            float last = 0f;
            return Make("whoosh", 0.6f, (t, rnd) =>
            {
                float n = (float)(rnd.NextDouble() * 2 - 1);
                last += (n - last) * 0.08f;
                return last * Mathf.Sin(Mathf.PI * t / 0.6f) * 2.2f;
            });
        }

        // 치익: 높은 잡음이 길게 빠진다(담금질 · 물)
        public static AudioClip Sizzle()
        {
            float last = 0f;
            return Make("sizzle", 1.4f, (t, rnd) =>
            {
                float n = (float)(rnd.NextDouble() * 2 - 1);
                float hp = n - last;
                last = n;
                return hp * Mathf.Exp(-t * 2.2f) * 0.35f;
            });
        }

        // 쇠가 타는 소리: 자글자글한 짧은 튀김
        public static AudioClip Crackle() => Make("crackle", 0.25f, (t, rnd) =>
            rnd.NextDouble() < 0.02 ? (float)(rnd.NextDouble() * 2 - 1) : 0f);

        // 작은 딸깍(단계 끝 · 버튼)
        public static AudioClip Chime() => Make("chime", 0.8f, (t, _) =>
            (Mathf.Sin(2 * Mathf.PI * 660f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 990f * t)) * Mathf.Exp(-t * 4f) * 0.3f);

        static AudioClip Make(string name, float seconds, System.Func<float, System.Random, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // 끝 10ms 를 줄여 딸깍 잡음을 없앤다
                float fade = Mathf.Clamp01((seconds - t) / 0.01f);
                data[i] = Mathf.Clamp(f(t, rnd) * fade, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
