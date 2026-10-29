using Blacksmith.UI;
using UnityEngine.UIElements;

namespace Blacksmith.Minigames
{
    // 아직 Unity 로 옮기지 않은 세부 단계. 점수를 직접 골라 넘어가서 공정 순서 전체를 돌려 볼 수 있게 한다.
    public sealed class PlaceholderMinigame : IMinigame
    {
        readonly string _partName;
        SliderInt _slider;

        public PlaceholderMinigame(string partName) => _partName = partName;

        public bool IsDone { get; private set; }
        public MinigameResult Result { get; private set; }

        public void Build(VisualElement root, MinigameContext ctx)
        {
            var panel = Ui.Box("panel", "placeholder");
            panel.Add(Ui.Label($"'{_partName}' 미니게임은 아직 Unity로 옮기지 않았습니다.", "h2"));
            panel.Add(Ui.Label("임시로 점수를 골라 다음으로 넘어갑니다.", "muted"));
            _slider = new SliderInt("점수", 0, 100) { value = 70, showInputField = true, focusable = false };
            _slider.AddToClassList("placeholder__slider");
            panel.Add(_slider);
            panel.Add(Ui.Button("이 점수로 넘어가기", Confirm, "btn-primary"));
            root.Add(panel);
        }

        void Confirm()
        {
            if (IsDone) return;
            Result = new MinigameResult { Score = _slider.value }.Add("placeholder", "임시 점수(미니게임 없음)", "예");
            IsDone = true;
        }

        public void Tick(float dt) { }
    }
}
