using System;
using System.Collections.Generic;
using Blacksmith.Crafting;

namespace Blacksmith.Activities
{
    // 세부 단계 → 3D 활동. PartDef.minigame 키로 먼저 찾고, 없으면 "공정키.단계키" 로 찾는다(예전 에셋은 minigame 이 비어 있다).
    // 둘 다 없으면 임시 점수 입력으로 돈다.
    public static class ActivityRegistry
    {
        static readonly Dictionary<string, Func<PartRef, IStationActivity>> ByMinigame = new()
        {
            ["bellows"] = p => new BellowsActivity(p),
            ["slag"] = p => new SlagActivity(p),
            ["fold"] = p => new FoldActivity(p),
        };

        static readonly Dictionary<string, Func<PartRef, IStationActivity>> ById = new()
        {
            ["smelt.slag"] = p => new SlagActivity(p),
            ["fold.sort"] = p => new SortActivity(p),
            ["shape.form"] = p => new FormActivity(p),
            ["shape.mark"] = p => new MarkActivity(p),
            ["shape.normalize"] = p => new NormalizeActivity(p),
            ["temper.quench"] = p => new QuenchActivity(p),
            ["temper.temper"] = p => new TemperActivity(p),
            ["grind.grind"] = p => new GrindActivity(p),
            ["grind.hone"] = p => new HoneActivity(p),
            ["handle.awl"] = p => new AwlActivity(p),
            ["handle.assemble"] = p => new AssembleActivity(p),
            ["handle.rivet"] = p => new RivetActivity(p),
            ["sheath.form"] = p => new WetFormActivity(p),
            ["sheath.stamp"] = p => new StampActivity(p),
            ["sheath.stitch"] = p => new StitchActivity(p),
        };

        // 달군 쇠로 해야 하는 단계(퀘스트 안내가 식었으면 화덕부터 들르게 한다)
        static readonly HashSet<string> HotWork = new() { "fold.fold", "shape.form", "shape.mark", "temper.quench" };

        public static bool NeedsHotPiece(PartRef p) => HotWork.Contains(p.Id);

        public static bool Has(PartRef p) =>
            (!string.IsNullOrEmpty(p.Part.minigame) && ByMinigame.ContainsKey(p.Part.minigame)) || ById.ContainsKey(p.Id);

        public static IStationActivity Create(PartRef part)
        {
            if (!string.IsNullOrEmpty(part.Part.minigame) && ByMinigame.TryGetValue(part.Part.minigame, out var f)) return f(part);
            if (ById.TryGetValue(part.Id, out f)) return f(part);
            return new PlaceholderActivity(part);
        }
    }
}
