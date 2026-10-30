using System;
using System.Collections.Generic;
using Blacksmith.Crafting;
using UnityEngine;

namespace Blacksmith.Economy
{
    // 손님 의뢰 하나: "예리함 60 이상 단검, 3일째까지, 보수 130실링"
    [Serializable]
    public class Order
    {
        public int id;
        public string customer;
        public string recipeKey;
        public string stat;        // sharpness · durability · beauty · total
        public int min;
        public int reward;
        public int dueDay;         // 이 날 저녁까지 납품
        public string note;        // 손님이 하는 말
    }

    // 창고의 재료(등급별 개수)
    [Serializable]
    public class Materials
    {
        public int oreLow, oreMid, oreHigh;
        public int coalPine, coalOak;
        public int leather, leatherFine;
        public int gripWood, gripBone, gripHorn;
    }

    // 한 자루에 쓴 재료(등급 키)
    [Serializable]
    public class MaterialPick
    {
        public string ore = "mid", coal = "pine", leather = "plain", grip = "wood";
    }

    // 경영 상태: 날짜 · 시각 · 돈 · 평판 · 의뢰 · 재료 · 업그레이드. SaveData 에 들어간다.
    [Serializable]
    public class EconomyState
    {
        public int day = 1;
        public int minutes = EconomyRules.DayStart;
        public int money = 60;
        public int reputation;
        public int nextOrderId = 1;
        public List<Order> offers = new();     // 오늘 찾아온 손님(아직 수락 · 거절 안 함)
        public List<Order> orders = new();     // 수락한 의뢰
        public Materials stock = new() { oreMid = 1, coalPine = 1, leather = 1, gripWood = 1 };
        public List<string> upgrades = new();
        public bool offersSpawned;             // 오늘 손님이 이미 왔는가
        public int earnedToday, deliveredToday;
    }

    // 경영 규칙과 값. 수치는 기획 초안 §2~§5 를 바탕으로 정했다.
    public static class EconomyRules
    {
        public const int DayStart = 8 * 60, DayEnd = 20 * 60;
        public const int MaxActiveOrders = 3;

        // 공정마다 드는 대장간 시간(분). 세부 단계는 가중치만큼 나눠 쓴다. 단검 한 자루 = 10시간.
        static readonly Dictionary<string, int> StageMinutes = new()
        {
            ["smelt"] = 120, ["fold"] = 90, ["shape"] = 120, ["temper"] = 60, ["grind"] = 90, ["handle"] = 60, ["sheath"] = 60,
        };

        public static int MinutesOf(PartRef p) =>
            Mathf.Max(10, Mathf.RoundToInt((StageMinutes.TryGetValue(p.Process.key, out var m) ? m : 60) * p.Part.weight / 10f) * 10);

        public static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

        // ── 재료 ─────────────────────────────
        public readonly struct MaterialDef
        {
            public readonly string Slot, Key, Name, Effect;
            public readonly int Price;
            public MaterialDef(string slot, string key, string name, int price, string effect)
            {
                Slot = slot; Key = key; Name = name; Price = price; Effect = effect;
            }
        }

        public static readonly MaterialDef[] MaterialDefs =
        {
            new("ore", "low", "하급 광석", 6, "제련 · 정련 점수 상한 80"),
            new("ore", "mid", "중급 광석", 10, "보정 없음"),
            new("ore", "high", "상급 광석", 18, "제련 · 정련 +5"),
            new("coal", "pine", "소나무 숯", 4, "보정 없음"),
            new("coal", "oak", "참나무 숯", 8, "제련 +3 (불이 오래 간다)"),
            new("leather", "plain", "송아지 가죽", 8, "보정 없음"),
            new("leather", "fine", "고운 가죽", 16, "칼집 +5"),
            new("grip", "wood", "나무 손잡이감", 3, "보정 없음"),
            new("grip", "bone", "뼈 손잡이감", 8, "손잡이 +4"),
            new("grip", "horn", "뿔 손잡이감", 14, "손잡이 +8"),
        };

        public static MaterialDef Mat(string slot, string key) => Array.Find(MaterialDefs, m => m.Slot == slot && m.Key == key);

        public static int Count(Materials s, string slot, string key) => (slot, key) switch
        {
            ("ore", "low") => s.oreLow, ("ore", "mid") => s.oreMid, ("ore", "high") => s.oreHigh,
            ("coal", "pine") => s.coalPine, ("coal", "oak") => s.coalOak,
            ("leather", "plain") => s.leather, ("leather", "fine") => s.leatherFine,
            ("grip", "wood") => s.gripWood, ("grip", "bone") => s.gripBone, ("grip", "horn") => s.gripHorn,
            _ => 0,
        };

        public static void Add(Materials s, string slot, string key, int n)
        {
            switch (slot, key)
            {
                case ("ore", "low"): s.oreLow += n; break;
                case ("ore", "mid"): s.oreMid += n; break;
                case ("ore", "high"): s.oreHigh += n; break;
                case ("coal", "pine"): s.coalPine += n; break;
                case ("coal", "oak"): s.coalOak += n; break;
                case ("leather", "plain"): s.leather += n; break;
                case ("leather", "fine"): s.leatherFine += n; break;
                case ("grip", "wood"): s.gripWood += n; break;
                case ("grip", "bone"): s.gripBone += n; break;
                case ("grip", "horn"): s.gripHorn += n; break;
            }
        }

        // 재료 등급이 세부 단계 점수에 주는 보정
        public static int ApplyMaterial(MaterialPick pick, PartRef p, int score, out string why)
        {
            why = null;
            if (pick == null) return score;
            string stage = p.Process.key;
            int s = score;
            if (stage is "smelt" or "fold")
            {
                if (pick.ore == "low" && s > 80) { s = 80; why = "하급 광석(상한 80)"; }
                if (pick.ore == "high") { s += 5; why = "상급 광석 +5"; }
            }
            if (stage == "smelt" && pick.coal == "oak") { s += 3; why = (why == null ? "" : why + " · ") + "참나무 숯 +3"; }
            if (stage == "sheath" && pick.leather == "fine") { s += 5; why = "고운 가죽 +5"; }
            if (stage == "handle" && pick.grip == "bone") { s += 4; why = "뼈 손잡이 +4"; }
            if (stage == "handle" && pick.grip == "horn") { s += 8; why = "뿔 손잡이 +8"; }
            return Mathf.Clamp(s, 0, 100);
        }

        // ── 업그레이드 ───────────────────────
        public readonly struct UpgradeDef
        {
            public readonly string Key, Name, Effect;
            public readonly int Price, Rep;
            public UpgradeDef(string key, string name, int price, int rep, string effect)
            {
                Key = key; Name = name; Price = price; Rep = rep; Effect = effect;
            }
        }

        // 효과는 MinigameContext 보정값(Mod)으로 활동에 들어간다
        public static readonly UpgradeDef[] Upgrades =
        {
            new("bellows", "큰 풀무", 120, 0, "풀무질 금색 칸 폭 100 → 140℃"),
            new("hammer", "좋은 망치", 140, 0, "접기 · 리벳 힘 구간 1.3배"),
            new("tongs", "단조 집게", 160, 3, "형상 · 마크 · 그립 구멍 떨림 0.7배"),
            new("tank", "물통 · 기름통", 110, 3, "담금질 · 뜨임 · 불림 판정 폭 1.35배"),
            new("whetstone", "고운 숫돌", 130, 6, "숫돌 과열 속도 0.65배"),
        };

        public static Dictionary<string, float> ModsOf(EconomyState eco)
        {
            var m = new Dictionary<string, float>();
            if (eco == null) return m;
            if (eco.upgrades.Contains("bellows")) m["bandHalf"] = 70f;
            if (eco.upgrades.Contains("hammer")) m["hammerHalf"] = 1.3f;
            if (eco.upgrades.Contains("tongs")) m["steady"] = 0.7f;
            if (eco.upgrades.Contains("tank")) m["colorHalf"] = 1.35f;
            if (eco.upgrades.Contains("whetstone")) m["grindHeat"] = 0.65f;
            return m;
        }

        // ── 의뢰 · 판매 ──────────────────────
        static readonly string[] Customers = { "떠돌이 기사 오스윈", "사냥꾼 브리타", "상인 길드의 마르코", "수도원 집사 베네딕트", "경비대장 하랄", "광부 톰", "귀족 아가씨 엘레노어", "순례자 아다", "용병 카스파르", "마을 촌장 그레타" };

        public static readonly Dictionary<string, string> StatNames = new()
            { ["sharpness"] = "예리함", ["durability"] = "강도", ["beauty"] = "장식미", ["total"] = "총점" };

        static readonly Dictionary<string, string[]> Notes = new()
        {
            ["sharpness"] = new[] { "가죽 갑옷 틈을 찌를 만큼 날카로워야 하오.", "고기를 뼈째 바를 칼이 필요해요." },
            ["durability"] = new[] { "전장에서 부러지지 않는 칼이면 됩니다.", "광산에서 막 써도 버티는 걸로요." },
            ["beauty"] = new[] { "의식에 쓸 칼이라 보기 좋아야 합니다.", "선물할 거라 손잡이와 칼집이 고와야 해요." },
            ["total"] = new[] { "두루 쓸 만한 단검 한 자루 부탁하오.", "솜씨를 보고 싶군요. 제대로 된 걸로." },
        };

        // 평판이 오를수록 조건이 까다롭고 보수가 크다
        public static Order MakeOrder(EconomyState eco, System.Random rng, string recipeKey)
        {
            string[] stats = { "sharpness", "durability", "beauty", "total" };
            string stat = stats[rng.Next(stats.Length)];
            int min = Mathf.Clamp(40 + eco.reputation * 2 + rng.Next(-5, 11), 35, 85);
            int reward = Mathf.RoundToInt(50 + min * 1.3f + rng.Next(0, 21));
            var notes = Notes[stat];
            return new Order
            {
                id = eco.nextOrderId++,
                customer = Customers[rng.Next(Customers.Length)],
                recipeKey = recipeKey,
                stat = stat,
                min = min,
                reward = reward,
                dueDay = eco.day + 2 + rng.Next(2),
                note = notes[rng.Next(notes.Length)],
            };
        }

        public static int OffersPerDay(EconomyState eco) => Mathf.Min(3, 2 + eco.reputation / 10);

        public static int StatOf(CraftRecord rec, string stat)
        {
            if (stat == "total") return rec.total;
            var s = Scoring.StatsOf(rec.stages);
            return stat switch { "sharpness" => s.Sharpness, "durability" => s.Durability, "beauty" => s.Beauty, _ => rec.total };
        }

        // 시장에 팔 때 값: 점수가 높을수록 가파르게 오른다
        public static int SellPrice(CraftRecord rec) => Mathf.RoundToInt(20 + rec.total * 0.9f + Mathf.Max(0, rec.total - 60) * 1.2f);

        // 의뢰 납품 보수: 조건을 넘긴 만큼 덤
        public static int RewardFor(Order o, CraftRecord rec) => o.reward + Mathf.Max(0, StatOf(rec, o.stat) - o.min);
    }
}
