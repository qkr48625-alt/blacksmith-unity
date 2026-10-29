using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blacksmith.Minigames
{
    // 정련 ① 접기 단접: 누르고 있으면 망치 힘이 차오르고, 떼는 순간의 힘으로 친다.
    // 5번 접는 동안 20번 두드린다. 수치는 웹 버전 FoldStage.jsx 와 같다.
    public sealed class FoldMinigame : IMinigame
    {
        const int Folds = 5;
        const int Blows = 4;
        const float FeedbackSeconds = 0.7f;
        const float ReheatSeconds = 1.4f;

        // 힘이 0에서 가득 차는 데 걸리는 시간(초). 가득 찬 뒤에도 누르고 있으면 다시 빠진다.
        // 접을수록 빨리 차오르고 금색 구간이 좁아진다.
        static float FillOf(int fold) => 1.2f - 0.08f * fold;
        static float HalfOf(int fold) => 0.12f - 0.012f * fold;

        System.Random _rng;
        float _ct, _pos, _target, _rest, _fbT;
        bool _charging;
        int _fold, _blow;
        int? _fbScore;
        readonly List<int> _scores = new();

        HoldInput _hold;
        Label _holdLabel, _count, _state, _word, _restLabel;
        VisualElement _bloom, _band, _fill, _needle, _gauge;
        readonly List<VisualElement> _pips = new();

        public bool IsDone { get; private set; }
        public MinigameResult Result { get; private set; }

        // 이번 타격에 필요한 힘: 55~85% 사이에서 매번 바뀐다
        float NewTarget() => 0.55f + (float)_rng.NextDouble() * 0.3f;

        public void Build(VisualElement root, MinigameContext ctx)
        {
            _rng = ctx.Rng;
            _target = NewTarget();

            var col = Ui.Box("fold");
            root.Add(col);

            var pips = Ui.Row("pips");
            for (int i = 0; i < Folds; i++)
            {
                var p = Ui.Box("pip");
                _pips.Add(p);
                pips.Add(p);
            }
            col.Add(pips);

            var status = Ui.Row("space-between");
            _count = Ui.Label("", "muted");
            _state = Ui.Label("", "muted");
            status.Add(_count);
            status.Add(_state);
            col.Add(status);

            // 블룸(철덩이)과 망치 판정
            var anvil = Ui.Box("panel", "anvil");
            _bloom = Ui.Box("bloom");
            _word = Ui.Label("", "anvil__word");
            _restLabel = Ui.Label("접어서 다시 달구는 중", "anvil__rest", "muted");
            anvil.Add(_bloom);
            anvil.Add(_word);
            anvil.Add(_restLabel);
            col.Add(anvil);

            col.Add(Ui.Label("망치 힘 — 누르고 있으면 차오릅니다. 금색 구간에서 손을 떼세요 (너무 오래 누르면 다시 빠집니다)", "small", "muted"));
            _gauge = Ui.Box("gauge");
            _band = Ui.Box("gauge__band");
            _fill = Ui.Box("gauge__fill");
            _needle = Ui.Box("gauge__needle");
            _gauge.Add(_fill);
            _gauge.Add(_band);
            _gauge.Add(_needle);
            col.Add(_gauge);

            var button = Ui.HoldButton("", out _holdLabel);
            button.AddToClassList("btn-hold--big");
            col.Add(button);
            col.Add(Ui.Label("스페이스바를 누르고 있다가 떼도 됩니다", "tiny", "muted", "align-center"));
            _hold = new HoldInput(button);

            Render();
        }

        public void Tick(float dt)
        {
            if (IsDone) return;
            _hold.Enabled = _rest <= 0f;
            _hold.Poll();
            if (_hold.Pressed) Press();
            if (_hold.Released) Release();
            if (IsDone) return;

            if (_rest > 0f) _rest -= dt;
            if (_charging)
            {
                _ct += dt;
                _pos = Gauge.Triangle(_ct, 2f * FillOf(_fold));
            }
            if (_fbT > 0f)
            {
                _fbT -= dt;
                if (_fbT <= 0f) _fbScore = null;
            }
            Render();
        }

        // 힘 모으기 시작
        void Press()
        {
            if (_rest > 0f || _fold >= Folds || _charging) return;
            _charging = true;
            _ct = 0f;
            _pos = 0f;
        }

        // 떼는 순간의 힘으로 판정한다
        void Release()
        {
            if (!_charging) return;
            _charging = false;
            int sc = Gauge.Score(_pos, _target, HalfOf(_fold));
            _scores.Add(sc);
            _fbScore = sc;
            _fbT = FeedbackSeconds;
            _target = NewTarget();
            _blow++;
            if (_blow < Blows) return;

            _blow = 0;
            _fold++;
            if (_fold == Folds)
            {
                Finish();
                return;
            }
            _rest = ReheatSeconds;
        }

        void Finish()
        {
            int avg = Scoring.Round(_scores.Average());
            var perFold = new List<int>();
            for (int i = 0; i < _scores.Count; i += Blows)
                perFold.Add(Scoring.Round(_scores.Skip(i).Take(Blows).Average()));
            Result = new MinigameResult { Score = avg }
                .Add("hitRate", "명중률(%)", Scoring.Round(_scores.Count(v => v >= 60) * 100.0 / _scores.Count))
                .Add("avg", "평균 정확도", avg)
                .Add("misses", "실패(25점 미만)", _scores.Count(v => v < 25))
                .Add("perFold", "접기별 평균", string.Join(" / ", perFold));
            _hold.Enabled = false;
            _hold.Reset();
            IsDone = true;
            Render();
        }

        void Render()
        {
            for (int i = 0; i < _pips.Count; i++) _pips[i].EnableInClassList("pip--on", i < _fold);
            _count.text = (_fold < Folds ? $"{_fold + 1}번째 접기" : "완료") + $" · 두드린 횟수 {_scores.Count}/{Folds * Blows}";
            _state.text = _rest > 0f ? "재가열 중…" : $"이번 접기 {_blow}/{Blows}";

            // 접을수록 블룸이 작고 단단해진다. 잘 맞으면 밝게 달아오르고 순간 찌그러진다.
            bool hit = _fbScore >= 60;
            _bloom.style.width = 120 - _fold * 8;
            _bloom.style.height = 48 - _fold * 3;
            _bloom.EnableInClassList("bloom--hit", _fbScore.HasValue && hit);
            float squash = _fbScore.HasValue && _fbT > FeedbackSeconds - 0.12f ? 0.92f : 1f;
            _bloom.style.scale = new Scale(new Vector3(squash, squash, 1f));
            _word.text = _fbScore.HasValue ? Gauge.HitWord(_fbScore.Value) : "";
            _restLabel.style.display = _rest > 0f ? DisplayStyle.Flex : DisplayStyle.None;

            float half = HalfOf(Mathf.Min(_fold, Folds - 1));
            _band.style.left = Length.Percent((_target - half) * 100f);
            _band.style.width = Length.Percent(half * 2f * 100f);
            Ui.SetPercentWidth(_fill, _pos);
            _needle.style.left = Length.Percent(_pos * 100f);
            _fill.EnableInClassList("gauge__fill--charging", _charging);
            _gauge.EnableInClassList("gauge--hit", _fbScore.HasValue && hit);
            _gauge.EnableInClassList("gauge--miss", _fbScore.HasValue && !hit);

            _holdLabel.text = _charging ? "힘 모으는 중… 떼면 내리칩니다" : "누르고 있다가 떼기";
        }
    }
}
