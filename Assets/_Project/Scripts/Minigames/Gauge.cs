using System;
using Blacksmith.Crafting;

namespace Blacksmith.Minigames
{
    // 힘 게이지 공용 계산. 웹 버전 engine/gauge.js 와 같다.
    // pos 는 0~1, center 는 금색 구간 가운데, half 는 구간 반폭.
    public static class Gauge
    {
        // 0 → 1 → 0 을 주기 period(초)로 되풀이하는 삼각파.
        public static float Triangle(float t, float period)
        {
            float x = t / period % 1f;
            return 1f - Math.Abs(2f * x - 1f);
        }

        // 구간 안이면 100(가운데)~60(가장자리), 바깥이라도 2.5배 폭까지는 60~0 으로 떨어진다.
        public static int Score(float pos, float center, float half)
        {
            float d = Math.Abs(pos - center);
            if (d <= half) return Scoring.Round(100 - 40 * (d / half));
            if (d <= 2.5f * half) return Scoring.Round(60 * (1 - (d - half) / (1.5f * half)));
            return 0;
        }

        public static string HitWord(int score)
        {
            if (score >= 85) return "완벽!";
            if (score >= 60) return "좋아요";
            if (score >= 25) return "빗맞음";
            return "실패";
        }
    }
}
