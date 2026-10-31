using System.Linq;
using Blacksmith.Activities;
using Blacksmith.Crafting;
using Blacksmith.Minigames;
using UnityEngine.InputSystem;

namespace Blacksmith.World
{
    // 에디터에서만 쓰는 점검용 단축키. 단계를 하나씩 확인할 때 앞 단계를 다시 하지 않게 한다.
    //   F5 = 지금 하던(없으면 다음) 세부 단계를 80점으로 건너뛰기   F6 = 들고 있는 쇠를 1,000℃로
    public partial class WorkshopGame
    {
#if UNITY_EDITOR
        // 점검용 자동 시작(Editor/AutoPlayLauncher): Temp/blacksmith-autoplay.txt 의 세부 단계까지 앞 단계를 80점으로 건너뛰고,
        // 쇠를 달군 채 그 작업대에 붙어 활동을 바로 시작한다. 파일은 읽고 지운다.
        void AutoStartForTest()
        {
            const string file = "Temp/blacksmith-autoplay.txt";
            if (!System.IO.File.Exists(file)) return;
            string target = System.IO.File.ReadAllText(file).Trim();
            System.IO.File.Delete(file);
            var recipe = recipes.FirstOrDefault(r => r != null);
            if (recipe == null) return;
            // 목표 단계가 할 수 있게 될 때까지 앞 단계를 건너뛴다(이미 지났으면 새로 시작)
            if (Recipe == null || CraftPlan.IsDone(_save.run, target)) StartRun(recipe);
            for (int guard = 0; guard < 30; guard++)
            {
                var avail = CraftPlan.Available(Recipe, _save.run);
                if (avail.Any(p => p.Id == target)) break;
                var next = avail.FirstOrDefault();
                if (next.Part == null) return;
                CompletePart(new SkippedActivity(next));
            }
            var part = CraftPlan.Available(Recipe, _save.run).FirstOrDefault(p => p.Id == target);
            if (part.Part == null) return;
            var station = StationOf(part.Part.station);
            if (station == null) return;
            if (_piece != null) _piece.Temperature = 1050f;
            _hud.HideOverlay();
            var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            agent.Warp(station.Approach.position);
            EnterStation(station);
            if (_activity == null) StartActivity(part);
            _hud.Toast($"[점검] {part.Part.name}에서 시작합니다");
        }

        void UpdateDebugKeys(Keyboard kb)
        {
            if (kb == null || _hud.OverlayOpen) return;
            if (kb.f5Key.wasPressedThisFrame) DebugSkip();
            if (kb.f6Key.wasPressedThisFrame && _piece != null)
            {
                _piece.Temperature = 1000f;
                _hud.Toast("[점검] 쇠를 1,000℃로");
            }
        }

        void DebugSkip()
        {
            var recipe = Recipe;
            if (recipe == null)
            {
                _hud.Toast("[점검] 먼저 게시판에서 만들 물건을 고르세요");
                return;
            }
            PartRef part;
            if (_activity != null)
            {
                part = _activity.Part;
                _activity.Exit();
                _activity = null;
            }
            else
            {
                var next = CraftPlan.Available(recipe, _save.run).FirstOrDefault();
                if (next.Part == null) return;
                part = next;
            }
            _inProgress.Remove(part.Id);
            _hud.Toast($"[점검] {part.Part.name} 건너뜀");
            CompletePart(new SkippedActivity(part));
        }

        // 건너뛴 단계: 바로 끝난 것으로, 80점
        sealed class SkippedActivity : IStationActivity
        {
            public SkippedActivity(PartRef part) => Part = part;
            public PartRef Part { get; }
            public string Hint => "";
            public void Begin(ActivityContext ctx) { }
            public void Enter() { }
            public void Exit() { }
            public void Tick(float dt) { }
            public bool IsDone => true;
            public MinigameResult Result { get; } = new MinigameResult { Score = 80 }.Add("skipped", "점검용 건너뛰기", "예");
        }
#else
        void AutoStartForTest() { }
        void UpdateDebugKeys(Keyboard kb) { }
#endif
    }
}
