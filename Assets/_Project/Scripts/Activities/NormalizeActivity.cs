using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 칼날 단조 ③ 불림: 누르고 있으면 칼날을 숯 속에 넣어 달구고, 떼면 화덕 앞 받침에 올려 공기 중에 식힌다.
    // 체리빛(옆의 견본과 같은 색)에서 떼고, 검게 식을 때까지 기다렸다가 다시 달구기를 3번. 덜 식었는데 달구면 그 회차 ×0.6.
    // 열은 웹 NormalizeStep.jsx 의 0~1 눈금을 300~1,300℃ 로 옮겼다(체리빛 0.5 = 800℃, 검게 식음 0.12 = 420℃).
    public sealed class NormalizeActivity : ActivityBase
    {
        const int Cycles = 3;
        const float HeatRate = 0.24f, Cherry = 0.5f, Half = 0.08f, Cold = 0.12f, Rush = 0.6f;

        struct Cycle { public float Peak; public bool Rushed; public int Score; }

        readonly List<Cycle> _cycles = new();
        float _h = 0.05f, _peak, _end;
        bool _heating, _rushed;
        Vector3 _inFire, _onRest;

        public NormalizeActivity(PartRef part) : base(part) { }

        static float TempOf(float h) => 300f + 1000f * h;

        public override string Hint
        {
            get
            {
                string s = _heating ? "달구는 중… 옆 견본처럼 체리빛이 되면 떼세요"
                    : _cycles.Count == 0 ? "누르고 있으면 숯 속에서 달궈집니다 (스페이스도 됨)"
                    : _h < Cold ? "검게 식었습니다. 다시 달구세요" : "공기 중에 식는 중… 검게 식을 때까지 기다리세요";
                return $"{s} · 불림 {_cycles.Count}/{Cycles}";
            }
        }

        protected override void Build()
        {
            var w = Station.WorkPoint.position;
            _inFire = w + Vector3.down * 0.02f;
            _onRest = w + ToStand * 0.62f + Vector3.down * 0.05f;
            // 화덕 앞 받침(쇠 두 갈래)
            var iron = ActivityKit.Mat(new Color(0.4f, 0.41f, 0.44f));
            var rest = ActivityKit.Root("Rest", Props, _onRest, FacingStand);
            ActivityKit.Prim(PrimitiveType.Cube, rest, new Vector3(-0.12f, -0.03f, 0f), new Vector3(0.02f, 0.05f, 0.06f), iron);
            ActivityKit.Prim(PrimitiveType.Cube, rest, new Vector3(0.12f, -0.03f, 0f), new Vector3(0.02f, 0.05f, 0.06f), iron);
            // 견본: 체리빛(목표) · 검게 식음
            var right = Vector3.Cross(Vector3.up, ToStand);
            HeatSamples.Glow(Props, _onRest + right * -0.25f + ToStand * 0.05f, FacingStand, TempOf(Cherry), true);
            HeatSamples.Glow(Props, _onRest + right * -0.25f + ToStand * 0.13f, FacingStand, TempOf(Cold) - 50f, false);
            // 받침 · 견본 · 숯불 가장자리가 함께 보이게
            Cam = ActivityKit.View(Station.transform, _onRest + ToStand * 0.6f + Vector3.up * 0.42f, _onRest - ToStand * 0.12f, 40f);
        }

        protected override void OnEnter()
        {
            Piece?.SetFire(null);
            if (Piece != null) PlacePiece(_heating ? _inFire : _onRest, FacingStand);
        }

        protected override void OnExit() => _heating = false;

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            if (_end > 0f)
            {
                Cool(dt);
                _end -= dt;
                if (_end <= 0f) Done();
                return;
            }
            bool down = Click.Down || Space.Down;
            bool held = Click.Held || Space.Held;
            if (!_heating && down && _cycles.Count < Cycles)
            {
                _heating = true;
                _rushed = _h >= Cold && _cycles.Count > 0;
                _peak = _h;
                FeedbackHub.Instance?.Whoosh(_inFire, 0.6f);
                PlacePiece(_inFire, FacingStand);
            }
            if (_heating)
            {
                _h = Mathf.Min(1f, _h + HeatRate * dt);
                _peak = _h;
                if (!held) Release();
            }
            else Cool(dt);
            if (Piece != null) Piece.Temperature = TempOf(_h);
        }

        void Cool(float dt)
        {
            _h = Mathf.Max(0.02f, _h - (0.06f + 0.15f * _h) * dt);
            if (Piece != null) Piece.Temperature = TempOf(_h);
        }

        void Release()
        {
            _heating = false;
            PlacePiece(_onRest, FacingStand);
            int baseSc = Gauge.Score(_peak, Cherry, Half);
            int sc = Scoring.Round(baseSc * (_rushed ? Rush : 1f));
            _cycles.Add(new Cycle { Peak = _peak, Rushed = _rushed, Score = sc });
            Toast(_rushed ? $"덜 식었는데 다시 달궜습니다 {sc}점"
                : baseSc >= 85 ? $"체리빛 딱 그 자리! {sc}점" : _peak < Cherry ? $"덜 달궜습니다 {sc}점" : $"너무 달궜습니다 {sc}점");
            if (_cycles.Count >= Cycles) _end = 1.2f;
        }

        void Done()
        {
            Finish(new MinigameResult { Score = Scoring.Round(_cycles.Average(c => c.Score)) }
                .Add("peaks", "뗀 온도(℃)", string.Join(" / ", _cycles.Select(c => Scoring.Round(TempOf(c.Peak)))))
                .Add("rushed", "덜 식고 다시 달굼", _cycles.Count(c => c.Rushed))
                .Add("cycles", "회차 점수", string.Join(" / ", _cycles.Select(c => c.Score))));
        }
    }
}
