using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Pieces;
using Blacksmith.Stations;
using Blacksmith.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 정련 ① 접기 단접 — "망치 세게 치기": 스페이스(또는 마우스)를 누르고 있으면 게이지가 아래에서 위로 쭉 올라가고,
    // 위로 갈수록 빨라진다. 떼면 그 높이의 힘으로 내리친다. 맨 위의 얇은 금색 선에서 떼면 최대 힘. 꼭대기를 넘으면 아래부터 다시.
    // 망치도 게이지만큼 들린다. 4번 치면 한 번 접힌다. 5번 접기. 접을수록 빨라지고 금색 선이 얇아진다.
    // 쇠가 체리빛 아래로 식으면 맞아도 반만 먹는다 — 화덕에 다녀와야 한다.
    public sealed class FoldActivity : IStationActivity
    {
        const int Folds = 5;
        const int Blows = 4;
        const float FoldAnimSeconds = 0.6f;
        const float StrikeSeconds = 0.3f;      // 내리친 뒤 잠깐 다음 타격을 받지 않는다

        // 오르는 빠르기(높이/초): 바닥에서 느리게 시작해 위로 갈수록 빨라진다. 접을수록 전체가 빨라진다.
        float SpeedAt(float pos) => (0.5f + 1.4f * pos * pos) * (1f + 0.1f * _fold);   // 꼭대기까지 약 1.2초, 꼭대기 근처 초당 1.9
        // 금색 선: 꼭대기에 붙은 얇은 띠. 반폭 0.04 → 0.03 (전에는 0.07, 좋은 망치면 1.3배)
        float HalfOf(int fold) => (0.04f - 0.0025f * fold) * _hammer;
        float TopCenter => 1f - HalfOf(_fold);
        float _hammer = 1f;

        AnvilStation _anvil;
        Workpiece _piece;
        float _sweep, _foldAnim, _wait;
        bool _active, _wasHeld = true, _charging;
        int _fold, _blow, _coldHits;
        readonly List<int> _scores = new();

        public FoldActivity(PartRef part) => Part = part;

        public PartRef Part { get; }
        public bool IsDone { get; private set; }
        public MinigameResult Result { get; private set; }

        public string Hint
        {
            get
            {
                string cold = _piece != null && _piece.Temperature < Workpiece.WorkableTemp
                    ? " · 쇠가 식었습니다! 화덕에서 달궈 오세요" : "";
                return $"스페이스(또는 마우스)를 누르고 있으면 게이지가 오릅니다 · 맨 위 금색 선에서 떼면 최대 힘! · {_fold + 1}/{Folds}번째 접기 {_blow}/{Blows}{cold}";
            }
        }

        public void Begin(ActivityContext ctx)
        {
            _anvil = ctx.Station as AnvilStation;
            _piece = ctx.Piece;
            _hammer = ctx.Game.Mod("hammerHalf", 1f);
            if (_anvil == null) Debug.LogError("[Fold] 모루가 아닌 작업대에서 시작했습니다.");
        }

        public void Enter()
        {
            _active = true;
            _wasHeld = true;
            _sweep = 0f;
            _charging = false;
            _anvil?.ShowTarget(TopCenter, HalfOf(_fold));
            _anvil?.SetPower(0f);
            ApplyShape();
        }

        public void Exit()
        {
            _active = false;
            _anvil?.SetPower(0f);
            _anvil?.SetMeter(-1f);
        }

        public void Tick(float dt)
        {
            if (IsDone || !_active || _anvil == null) return;

            bool held = (Mouse.current != null && Mouse.current.leftButton.isPressed && !WorkshopGame.PointerOverUi)
                        || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            // 새로 눌렀을 때만 친다(작업대를 고른 클릭이 그대로 망치질이 되지 않게)
            bool pressed = held && !_wasHeld;
            _wasHeld = held;

            if (_foldAnim > 0f)
            {
                _foldAnim -= dt;
                ApplyShape();
                return;
            }
            if (_wait > 0f)
            {
                _wait -= dt;
                return;
            }

            if (pressed && !_charging)
            {
                _charging = true;
                _sweep = 0f;
            }
            if (!_charging) return;
            // 누르고 있는 동안 위로 갈수록 빨라지며 오르고, 꼭대기를 넘으면 아래부터 다시
            _sweep += SpeedAt(_sweep) * dt;
            if (_sweep > 1f) _sweep = 0f;
            _anvil.SetPower(_sweep);
            if (!held) Strike(_sweep);
        }

        void Strike(float pos)
        {
            int sc = Gauge.Score(pos, TopCenter, HalfOf(_fold));
            bool cold = _piece != null && _piece.Temperature < Workpiece.WorkableTemp;
            if (cold)
            {
                sc = Scoring.Round(sc * 0.5);
                _coldHits++;
            }
            _scores.Add(sc);
            // 게이지 높이만큼 세게 내리친다
            _charging = false;
            _anvil.Strike(Mathf.Max(0.3f, pos));
            _wait = StrikeSeconds;
            _sweep = 0f;

            var hitPos = _piece != null ? _piece.transform.position : _anvil.WorkPoint.position;
            FeedbackHub.Instance?.Hit(hitPos + Random.insideUnitSphere * 0.03f, cold ? Mathf.Min(sc, 20) : sc);

            _blow++;
            if (_blow == Blows)
            {
                _blow = 0;
                _fold++;
                if (_fold == Folds)
                {
                    Finish();
                    return;
                }
                _foldAnim = FoldAnimSeconds;
            }
            _anvil.ShowTarget(TopCenter, HalfOf(_fold));
            ApplyShape();
        }

        // 두드릴수록 블룸이 납작하게 늘어나고, 접으면 다시 짧고 두꺼워진다
        void ApplyShape()
        {
            if (_piece == null) return;
            float spread = _blow / (float)Blows;
            float folding = _foldAnim > 0f ? 1f - _foldAnim / FoldAnimSeconds : 0f;
            float len = Mathf.Lerp(1f + 0.5f * spread, 1f, folding) + 0.12f * _fold;
            float thick = Mathf.Lerp(1f - 0.3f * spread, 1f, folding) - 0.05f * _fold;
            _piece.transform.localScale = new Vector3(len, Mathf.Max(0.4f, thick), 1f);
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
                .Add("coldHits", "식은 채로 친 횟수", _coldHits)
                .Add("perFold", "접기별 평균", string.Join(" / ", perFold));
            if (_piece != null) _piece.transform.localScale = Vector3.one;
            _anvil.SetGaugeVisible(false);
            _anvil.SetMeter(-1f);
            _anvil.SetPower(0f);
            IsDone = true;
        }
    }
}
