using System.Collections.Generic;
using Blacksmith.Crafting;
using UnityEngine.UIElements;

namespace Blacksmith.Minigames
{
    // 미니게임이 시작할 때 받는 값. 나중에 업그레이드 · 숙련 · 재료 보정이 Mods 로 들어온다.
    public sealed class MinigameContext
    {
        public readonly int Seed;
        public readonly System.Random Rng;
        readonly Dictionary<string, float> _mods;

        public MinigameContext(int seed, Dictionary<string, float> mods = null)
        {
            Seed = seed;
            Rng = new System.Random(seed);
            _mods = mods ?? new Dictionary<string, float>();
        }

        public float Mod(string key, float fallback = 0f) => _mods.TryGetValue(key, out var v) ? v : fallback;
    }

    public sealed class MinigameResult
    {
        public int Score;
        public List<DetailEntry> Detail = new();

        public MinigameResult Add(string key, string label, object value)
        {
            Detail.Add(new DetailEntry(key, label, value.ToString()));
            return this;
        }
    }

    // 공정 안 세부 단계 하나. Build 로 화면을 그리고, 매 프레임 Tick 을 받다가 IsDone 이 되면 Result 를 낸다.
    public interface IMinigame
    {
        void Build(VisualElement root, MinigameContext ctx);
        void Tick(float dt);
        bool IsDone { get; }
        MinigameResult Result { get; }
    }
}
