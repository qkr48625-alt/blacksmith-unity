using System;
using System.Collections.Generic;

namespace Blacksmith.Crafting
{
    // 측정값 한 줄. 검증표에 쓴다. JsonUtility 가 Dictionary 를 못 다뤄서 목록으로 둔다.
    [Serializable]
    public class DetailEntry
    {
        public string key;
        public string label;
        public string value;

        public DetailEntry(string key, string label, string value)
        {
            this.key = key;
            this.label = label;
            this.value = value;
        }
    }

    [Serializable]
    public class PartRecord
    {
        public string key;
        public int score;
        public List<DetailEntry> detail = new();
    }

    [Serializable]
    public class StageRecord
    {
        public string key;
        public int score;
        public List<PartRecord> parts = new();

        public PartRecord Part(string partKey) => parts.Find(p => p.key == partKey);
    }

    // 진행 중인 한 자루. 세부 단계가 끝날 때마다 저장해서, 껐다 켜도 다음 세부 단계부터 이어 한다.
    [Serializable]
    public class CraftRun
    {
        public string recipeKey;
        public int seed;
        public List<StageRecord> stages = new();
        public string startedAt;
        // 3D 대장간: 작업물 상태(WorkpieceState 이름, 비면 아직 없음)와 지금까지 탄 시간(초)
        public string workpieceState;
        public float burnTotal;

        public StageRecord Stage(string stageKey) => stages.Find(s => s.key == stageKey);

        public bool IsDone(string stageKey, string partKey) => Stage(stageKey)?.Part(partKey) != null;

        // 지금 하고 있는(아직 점수가 안 난) 공정 번호. stages 마지막이 덜 끝났으면 그 공정이다.
        public int CurrentStageIndex(RecipeDef recipe)
        {
            if (stages.Count == 0) return 0;
            var last = stages[stages.Count - 1];
            var def = recipe.stages[stages.Count - 1].process;
            return last.parts.Count < def.parts.Count ? stages.Count - 1 : stages.Count;
        }
    }

    // 완성한 한 자루. 보관함에 쌓인다.
    [Serializable]
    public class CraftRecord
    {
        public string recipeKey;
        public string recipeName;
        public int seed;
        public int total;
        public string tierKey;
        public List<StageRecord> stages = new();
        public string finishedAt;
        // 경영: 시장에 팔았는가, 어느 의뢰에 납품했는가(0 = 아직)
        public bool sold;
        public int deliveredTo;
        public int day;

        public bool InStock => !sold && deliveredTo == 0;
    }
}
