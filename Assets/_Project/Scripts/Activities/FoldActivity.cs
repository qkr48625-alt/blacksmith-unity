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
    // 정련 ① 접기 단접: 마우스(또는 스페이스)를 누르고 있으면 망치가 올라가고, 떼면 내리친다.
    // 올라간 높이가 힘이고, 모루 옆 눈금 기둥의 금색 띠가 이번에 필요한 힘이다. 4번 치면 한 번 접힌다. 5번 접기.
    // 힘 · 구간 수치는 웹 버전 FoldStage.jsx 와 같다. 쇠가 체리빛 아래로 식으면 맞아도 반만 먹는다 — 화덕에 다녀와야 한다.
    public sealed class FoldActivity : IStationActivity
    {
        const int Folds = 5;
        const int Blows = 4;
        const float FoldAnimSeconds = 0.6f;

        static float FillOf(int fold) => 1.2f - 0.08f * fold;
        static float HalfOf(int fold) => 0.12f - 0.012f * fold;

        AnvilStation _anvil;
        Workpiece _piece;
        System.Random _rng;
        float _ct, _pos, _target, _foldAnim;
        bool _charging, _active, _wasHeld = true;
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
                return $"누르고 있다가 떼서 내리치기 (스페이스도 됨) · 망치 높이를 기둥의 금색 띠에 맞추세요 · {_fold + 1}/{Folds}번째 접기 {_blow}/{Blows}{cold}";
            }
        }

        float NewTarget() => 0.55f + (float)_rng.NextDouble() * 0.3f;

        public void Begin(ActivityContext ctx)
        {
            _anvil = ctx.Station as AnvilStation;
            _piece = ctx.Piece;
            _rng = ctx.Game.Rng;
            _target = NewTarget();
            if (_anvil == null) Debug.LogError("[Fold] 모루가 아닌 작업대에서 시작했습니다.");
        }

        public void Enter()
        {
            _active = true;
            _charging = false;
            _wasHeld = true;
            _anvil?.ShowTarget(_target, HalfOf(_fold));
            ApplyShape();
        }

        public void Exit()
        {
            _active = false;
            _charging = false;
            _anvil?.SetPower(0f);
        }

        public void Tick(float dt)
        {
            if (IsDone || !_active || _anvil == null) return;

            if (_foldAnim > 0f)
            {
                _foldAnim -= dt;
                ApplyShape();
                return;
            }

            bool held = (Mouse.current != null && Mouse.current.leftButton.isPressed && !WorkshopGame.PointerOverUi)
                        || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            // 새로 눌렀을 때만 들기 시작한다(작업대를 고른 클릭이 그대로 망치질이 되지 않게)
            bool pressed = held && !_wasHeld;
            _wasHeld = held;
            if (pressed && !_charging)
            {
                _charging = true;
                _ct = 0f;
            }
            if (_charging)
            {
                _ct += dt;
                // 가득 찬 뒤에도 들고 있으면 팔이 떨려 다시 내려온다(삼각파)
                _pos = Gauge.Triangle(_ct, 2f * FillOf(_fold));
                _anvil.SetPower(_pos);
                if (!held) Strike();
            }
        }

        void Strike()
        {
            _charging = false;
            int sc = Gauge.Score(_pos, _target, HalfOf(_fold));
            bool cold = _piece != null && _piece.Temperature < Workpiece.WorkableTemp;
            if (cold)
            {
                sc = Scoring.Round(sc * 0.5);
                _coldHits++;
            }
            _scores.Add(sc);
            _anvil.Strike();

            var hitPos = _piece != null ? _piece.transform.position : _anvil.WorkPoint.position;
            FeedbackHub.Instance?.Hit(hitPos + Random.insideUnitSphere * 0.03f, cold ? Mathf.Min(sc, 20) : sc);

            _target = NewTarget();
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
            _anvil.ShowTarget(_target, HalfOf(_fold));
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
            IsDone = true;
        }
    }
}
