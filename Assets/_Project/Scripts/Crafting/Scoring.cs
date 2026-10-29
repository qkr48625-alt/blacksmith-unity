using System;
using System.Collections.Generic;

namespace Blacksmith.Crafting
{
    public readonly struct Tier
    {
        public readonly string Key;
        public readonly int Min;
        public readonly string Name;
        public readonly string Blurb;

        public Tier(string key, int min, string name, string blurb)
        {
            Key = key;
            Min = min;
            Name = name;
            Blurb = blurb;
        }
    }

    public readonly struct Stats
    {
        public readonly int Sharpness;
        public readonly int Durability;
        public readonly int Beauty;
        public readonly int Attack;

        public Stats(int sharpness, int durability, int beauty, int attack)
        {
            Sharpness = sharpness;
            Durability = durability;
            Beauty = beauty;
            Attack = attack;
        }
    }

    // 공정 점수 → 합계 · 등급 · 능력치. 웹 버전 engine/result.js 와 같은 식이다.
    public static class Scoring
    {
        // 점수 등급. Min 이상이면 그 등급이다.
        public static readonly Tier[] Tiers =
        {
            new("legend", 85, "전설의 검", "칼날이 빛을 머금고, 제작자 마크에 금이 입혀진다."),
            new("famous", 72, "명검", "결이 살아 있고 날 선이 맑다. 누가 봐도 잘 만든 칼."),
            new("fine", 58, "정교한 검", "흠이 적고 모양이 맞는다. 쓰기에도 무난하다."),
            new("decent", 40, "쓸 만한 검", "쓰는 데는 문제없지만 장식은 아쉽다."),
            new("crude", 0, "조잡한 검", "쇠가 덜 익고 날이 비뚤다. 다시 만들어야 한다."),
        };

        // JS Math.round 와 같게 .5 는 올린다(Mathf.RoundToInt 는 짝수 쪽으로 반올림한다).
        public static int Round(double v) => (int)Math.Floor(v + 0.5);

        public static int PartsScore(ProcessDef process, StageRecord rec)
        {
            double sum = 0;
            foreach (var p in process.parts) sum += p.weight * (rec.Part(p.key)?.score ?? 0);
            return Round(sum);
        }

        public static int Total(RecipeDef recipe, IReadOnlyList<StageRecord> stages)
        {
            double sum = 0;
            foreach (var st in recipe.stages)
            {
                var rec = Find(stages, st.process.key);
                sum += st.weight * (rec?.score ?? 0);
            }
            return Round(sum);
        }

        public static Tier TierOf(int total)
        {
            foreach (var t in Tiers) if (total >= t.Min) return t;
            return Tiers[^1];
        }

        public static Tier TierByKey(string key) => Array.Find(Tiers, t => t.Key == key);

        // 예리함은 연마 · 단조, 강도는 제련 · 정련 · 열처리, 장식미는 손잡이 · 칼집이 맡는다.
        public static Stats StatsOf(IReadOnlyList<StageRecord> stages)
        {
            int S(string k) => Find(stages, k)?.score ?? 0;
            int sharpness = Round(0.5 * S("grind") + 0.5 * S("shape"));
            int durability = Round(0.4 * S("smelt") + 0.3 * S("fold") + 0.3 * S("temper"));
            int beauty = Round(0.5 * S("handle") + 0.5 * S("sheath"));
            int attack = Round(0.6 * sharpness + 0.4 * durability);
            return new Stats(sharpness, durability, beauty, attack);
        }

        static StageRecord Find(IReadOnlyList<StageRecord> stages, string key)
        {
            foreach (var s in stages) if (s.key == key) return s;
            return null;
        }
    }
}
