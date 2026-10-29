using System.Collections.Generic;

namespace Blacksmith.Crafting
{
    // 레시피의 세부 단계 하나를 가리킨다. Id 는 "공정키.단계키".
    public readonly struct PartRef
    {
        public readonly RecipeStage Stage;
        public readonly PartDef Part;

        public PartRef(RecipeStage stage, PartDef part)
        {
            Stage = stage;
            Part = part;
        }

        public ProcessDef Process => Stage.process;
        public string Id => MakeId(Process.key, Part.key);

        public static string MakeId(string stageKey, string partKey) => $"{stageKey}.{partKey}";
    }

    // 선행 관계로 짠 공정표. 순서를 강제하지 않고, requires 가 다 끝난 세부 단계는 어느 것부터 해도 된다.
    public static class CraftPlan
    {
        public static IEnumerable<PartRef> AllParts(RecipeDef recipe)
        {
            foreach (var st in recipe.stages)
                foreach (var p in st.process.parts)
                    yield return new PartRef(st, p);
        }

        public static bool IsDone(CraftRun run, PartRef p) => run.IsDone(p.Process.key, p.Part.key);

        public static bool IsDone(CraftRun run, string id)
        {
            int dot = id.IndexOf('.');
            return dot > 0 && run.IsDone(id[..dot], id[(dot + 1)..]);
        }

        public static bool IsUnlocked(CraftRun run, PartRef p)
        {
            foreach (var req in p.Part.requires)
                if (!IsDone(run, req)) return false;
            return true;
        }

        // 지금 할 수 있는(선행이 다 끝났고 아직 안 한) 세부 단계
        public static List<PartRef> Available(RecipeDef recipe, CraftRun run)
        {
            var list = new List<PartRef>();
            foreach (var p in AllParts(recipe))
                if (!IsDone(run, p) && IsUnlocked(run, p)) list.Add(p);
            return list;
        }

        public static bool IsComplete(RecipeDef recipe, CraftRun run)
        {
            foreach (var p in AllParts(recipe))
                if (!IsDone(run, p)) return false;
            return true;
        }

        public static int DoneCount(RecipeDef recipe, CraftRun run)
        {
            int n = 0;
            foreach (var p in AllParts(recipe))
                if (IsDone(run, p)) n++;
            return n;
        }

        public static int PartCount(RecipeDef recipe)
        {
            int n = 0;
            foreach (var st in recipe.stages) n += st.process.parts.Count;
            return n;
        }

        // 세부 단계 하나를 기록한다. 그 공정의 단계가 다 끝나면 공정 점수도 매긴다.
        public static StageRecord Record(CraftRun run, PartRef p, PartRecord rec)
        {
            var stage = run.Stage(p.Process.key);
            if (stage == null)
            {
                stage = new StageRecord { key = p.Process.key };
                run.stages.Add(stage);
            }
            stage.parts.RemoveAll(x => x.key == rec.key);
            stage.parts.Add(rec);
            if (stage.parts.Count >= p.Process.parts.Count) stage.score = Scoring.PartsScore(p.Process, stage);
            return stage;
        }
    }
}
