using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 열처리 ② 뜨임: 담금질한 칼날을 화덕 앞 뜨거운 받침에 올리면 산화막 색이 짚색 → 갈색 → 보라 → 청색으로 번진다(16초).
    // 금 테를 두른 견본 색에 닿았을 때 클릭(또는 스페이스)으로 집어 올린다. 18초 안에. 수치는 웹 TemperStage.jsx 와 같다.
    public sealed class TemperActivity : ActivityBase
    {
        const float Seconds = 16f, Limit = 18f, Half = 0.06f;
        static readonly (float v, Color c)[] Stops =
        {
            (0f, new Color(0.96f, 0.9f, 0.66f)),
            (0.33f, new Color(0.6f, 0.36f, 0.12f)),
            (0.66f, new Color(0.42f, 0.18f, 0.63f)),
            (1f, new Color(0.17f, 0.34f, 0.79f)),
        };
        static readonly string[] Names = { "짚색", "갈색", "보라", "청색" };
        static readonly float[] Targets = { 0.1f, 0.33f, 0.66f, 0.9f };

        int _targetIdx;
        float _v, _end;
        bool _picked;

        public TemperActivity(PartRef part) : base(part) { }

        public override string Hint => _picked ? "뜨임 끝"
            : $"칼날 색이 금 테 견본({Names[_targetIdx]})과 같아지면 클릭(또는 스페이스)으로 집어 올리기 · 남은 {Mathf.CeilToInt(Mathf.Max(0f, Limit - T))}초";

        static Color ColorAt(float v)
        {
            for (int i = 1; i < Stops.Length; i++)
                if (v <= Stops[i].v) return Color.Lerp(Stops[i - 1].c, Stops[i].c, Mathf.InverseLerp(Stops[i - 1].v, Stops[i].v, v));
            return Stops[^1].c;
        }

        protected override void Build()
        {
            _targetIdx = 1 + Rng.Next(2);   // 단검은 갈색 · 보라 사이가 알맞다
            var w = Station.WorkPoint.position;
            var rest = w + ToStand * 0.62f + Vector3.down * 0.05f;
            var plate = ActivityKit.Root("Hot Plate", Props, rest + Vector3.down * 0.02f, FacingStand);
            ActivityKit.Prim(PrimitiveType.Cube, plate, Vector3.zero, new Vector3(0.5f, 0.02f, 0.12f), ActivityKit.Mat(new Color(0.2f, 0.18f, 0.17f), new Color(0.5f, 0.12f, 0.02f) * 0.6f));
            // 견본 넷(목표에 금 테)
            var right = Vector3.Cross(Vector3.up, ToStand);
            for (int i = 0; i < 4; i++)
                HeatSamples.Tint(Props, rest + ToStand * 0.13f + right * (-0.12f + i * 0.08f), FacingStand, ColorAt(Targets[i]), i == _targetIdx);
            Cam = ActivityKit.View(Station.transform, rest + ToStand * 0.55f + Vector3.up * 0.4f, rest + ToStand * 0.05f, 38f);
        }

        protected override void OnEnter()
        {
            Piece?.SetFire(null);
            var rest = Station.WorkPoint.position + ToStand * 0.62f + Vector3.down * 0.03f;
            PlacePiece(rest, FacingStand);
            Piece?.SetTint(ColorAt(_v));
        }

        protected override void OnExit()
        {
            if (!_picked) Piece?.SetTint(null);
        }

        protected override void Tick()
        {
            if (_end > 0f)
            {
                _end -= Time.deltaTime;
                if (_end <= 0f) Done();
                return;
            }
            _v = Mathf.Min(1f, T / Seconds);
            if (Piece != null)
            {
                Piece.SetTint(ColorAt(_v));
                Piece.Temperature = Mathf.Lerp(200f, 320f, _v);   // 뜨임 온도: 빛나지 않는다
            }
            if (Click.Down || Space.Down || T >= Limit) Pick();
        }

        void Pick()
        {
            _picked = true;
            int sc = Gauge.Score(_v, Targets[_targetIdx], Half);
            FeedbackHub.Instance?.Poke(Piece != null ? Piece.transform.position : Station.WorkPoint.position);
            Toast(T >= Limit ? $"너무 오래 두었습니다 {sc}점" : sc >= 85 ? $"{Names[_targetIdx]} 딱 맞췄습니다! {sc}점" : _v < Targets[_targetIdx] ? $"일렀습니다 {sc}점" : $"늦었습니다 {sc}점");
            _end = 1.2f;
            _score = sc;
        }

        int _score;

        void Done()
        {
            Finish(new MinigameResult { Score = _score }
                .Add("target", "목표 색", Names[_targetIdx])
                .Add("off", "목표와 차이(%)", Scoring.Round(Mathf.Abs(_v - Targets[_targetIdx]) * 100f)));
        }
    }
}
