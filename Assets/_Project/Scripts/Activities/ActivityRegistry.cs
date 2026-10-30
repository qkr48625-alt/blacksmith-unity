using System;
using System.Collections.Generic;
using Blacksmith.Crafting;

namespace Blacksmith.Activities
{
    // PartDef.minigame 키 → 3D 활동. 아직 만들지 않은 단계는 임시 점수 입력으로 돈다.
    public static class ActivityRegistry
    {
        static readonly Dictionary<string, Func<PartRef, IStationActivity>> Factories = new()
        {
            ["bellows"] = p => new BellowsActivity(p),
            ["slag"] = p => new SlagActivity(p),
            ["fold"] = p => new FoldActivity(p),
        };

        public static bool Has(string key) => !string.IsNullOrEmpty(key) && Factories.ContainsKey(key);

        public static IStationActivity Create(PartRef part) =>
            Has(part.Part.minigame) ? Factories[part.Part.minigame](part) : new PlaceholderActivity(part);
    }
}
