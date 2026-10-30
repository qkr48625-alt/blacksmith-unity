using System.Linq;
using Blacksmith.Activities;
using Blacksmith.Crafting;
using Blacksmith.Economy;
using Blacksmith.Minigames;
using UnityEngine.InputSystem;

namespace Blacksmith.World
{
    // 에디터에서만 쓰는 점검용 단축키. 단계를 하나씩 확인할 때 앞 단계를 다시 하지 않게 한다.
    //   F5 = 지금 하던(없으면 다음) 세부 단계를 80점으로 건너뛰기   F6 = 들고 있는 쇠를 1,000℃로
    //   F7 = 돈 +100 · 평판 +5                                    F8 = 시각을 아침 8시로
    public partial class WorkshopGame
    {
#if UNITY_EDITOR
        void UpdateDebugKeys(Keyboard kb)
        {
            if (kb == null || _hud.OverlayOpen) return;
            if (kb.f5Key.wasPressedThisFrame) DebugSkip();
            if (kb.f6Key.wasPressedThisFrame && _piece != null)
            {
                _piece.Temperature = 1000f;
                _hud.Toast("[점검] 쇠를 1,000℃로");
            }
            if (kb.f7Key.wasPressedThisFrame)
            {
                Eco.money += 100;
                Eco.reputation += 5;
                Persist();
                _hud.Toast("[점검] 돈 +100 · 평판 +5");
            }
            if (kb.f8Key.wasPressedThisFrame)
            {
                Eco.minutes = EconomyRules.DayStart;
                Persist();
                _hud.Toast("[점검] 아침 8시로");
            }
        }

        void DebugSkip()
        {
            var recipe = Recipe;
            if (recipe == null)
            {
                _hud.Toast("[점검] 먼저 게시판에서 만들기를 시작하세요");
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
        void UpdateDebugKeys(Keyboard kb) { }
#endif
    }
}
