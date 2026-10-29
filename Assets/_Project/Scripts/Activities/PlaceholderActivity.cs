using Blacksmith.Crafting;
using Blacksmith.Minigames;
using Blacksmith.UI;
using UnityEngine.UIElements;

namespace Blacksmith.Activities
{
    // 아직 3D 로 만들지 않은 세부 단계. 작업대에 붙으면 HUD 에 점수 고르기 창을 띄운다(1차 UI 의 PlaceholderMinigame 재사용).
    public sealed class PlaceholderActivity : IStationActivity
    {
        readonly PlaceholderMinigame _game;
        VisualElement _panel;
        Hud _hud;

        public PlaceholderActivity(PartRef part)
        {
            Part = part;
            _game = new PlaceholderMinigame(part.Part.name);
        }

        public PartRef Part { get; }
        public string Hint => "임시 단계: 점수를 골라 넘어가세요";
        public bool IsDone => _game.IsDone;
        public MinigameResult Result => _game.Result;

        public void Begin(ActivityContext ctx)
        {
            _hud = ctx.Hud;
            _panel = new VisualElement();
            _game.Build(_panel, ctx.Game);
        }

        public void Enter() => _hud.ShowPanel(_panel);

        public void Exit() => _hud.HidePanel(_panel);

        public void Tick(float dt)
        {
            if (IsDone) _hud.HidePanel(_panel);
        }
    }
}
