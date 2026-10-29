using System;
using System.Collections.Generic;

namespace Blacksmith.Minigames
{
    // PartDef.minigame 키 → 미니게임. 아직 옮기지 않은 단계는 임시 점수 입력(PlaceholderMinigame)으로 돈다.
    public static class MinigameRegistry
    {
        static readonly Dictionary<string, Func<IMinigame>> Factories = new()
        {
            ["bellows"] = () => new BellowsMinigame(),
            ["fold"] = () => new FoldMinigame(),
        };

        public static bool Has(string key) => !string.IsNullOrEmpty(key) && Factories.ContainsKey(key);

        public static IMinigame Create(string key, string partName) =>
            Has(key) ? Factories[key]() : new PlaceholderMinigame(partName);
    }
}
