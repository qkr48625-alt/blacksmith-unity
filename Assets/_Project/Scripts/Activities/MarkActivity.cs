using System.Collections.Generic;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Stations;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 칼날 단조 ② 마크 각인: 칼날 뿌리의 점선 원에 떨리는 펀치를 맞춰 클릭 두 번. 같은 자리를 두 번 쳐야 깊고 또렷하다.
    // 점수 = 자리 40% + 두 번째가 첫 번째에 겹친 정도 30% + 찍을 때 쇠 온도 30%(600℃ 아래 0, 800℃ 이상 만점).
    // 찍힌 자국은 작업물에 그대로 남는다. 수치는 웹 MarkStep.jsx 를 m 로 옮겼다.
    public sealed class MarkActivity : ActivityBase
    {
        const float Unit = 0.4f / 206f;
        const float Jitter = 6f * Unit;

        AnvilStation _anvil;
        Transform _punch, _ring;
        readonly List<(Vector3 pos, float temp)> _strikes = new();
        Vector3 _aim;
        Plane _plane;
        float _end;

        public MarkActivity(PartRef part) : base(part) { }

        Vector3 TargetPos => Station.WorkPoint.position + new Vector3(-0.13f, 0.03f, 0f);

        public override string Hint
        {
            get
            {
                float temp = Piece != null ? Piece.Temperature : 0f;
                string heat = temp >= 800f ? "아직 뜨겁습니다" : temp > 600f ? "식어 갑니다!" : "다 식었습니다 — 얕게 찍힙니다";
                return $"떨리는 펀치를 점선 원 가운데에 맞춰 클릭, 첫 자국 위를 한 번 더 · 펀치 {_strikes.Count}/2 · {heat}";
            }
        }

        protected override void Build()
        {
            _anvil = Station as AnvilStation;
            _plane = new Plane(Vector3.up, TargetPos);
            // 점선 원
            _ring = new GameObject("Ring").transform;
            _ring.SetParent(Props, false);
            _ring.position = TargetPos + Vector3.up * 0.001f;
            var gold = ActivityKit.Mat(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.75f, 0.3f) * 1.8f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                ActivityKit.Prim(PrimitiveType.Cube, _ring, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.027f), new Vector3(0.006f, 0.001f, 0.002f), gold, Quaternion.Euler(0f, a, 0f));
            }
            // 펀치: 위에서 내려다보면 끝(작은 원)이 보이는 쇠 막대
            _punch = new GameObject("Punch").transform;
            _punch.SetParent(Props, false);
            var iron = ActivityKit.Mat(new Color(0.55f, 0.56f, 0.6f));
            ActivityKit.Prim(PrimitiveType.Cylinder, _punch, new Vector3(0f, 0.09f, 0f), new Vector3(0.012f, 0.08f, 0.012f), iron);
            ActivityKit.Prim(PrimitiveType.Cylinder, _punch, new Vector3(0f, 0.004f, 0f), new Vector3(0.016f, 0.004f, 0.016f), ActivityKit.Mat(Color.white, Color.white));
            MakeView(0.4f, 0.45f, new Vector3(-0.13f, 0f, 0f), 28f);
        }

        protected override void OnEnter() => _anvil?.SetPower(0.85f);
        protected override void OnExit() => _anvil?.SetPower(0f);

        protected override void Tick()
        {
            _anvil?.SetPower(0.85f);
            if (_end > 0f)
            {
                _end -= Time.deltaTime;
                if (_end <= 0f) Done();
                return;
            }
            if (ActivityKit.Aim(_plane, out var p)) _aim = p;
            var k = ActivityKit.Tremor(T, Jitter);
            var hit = _aim + new Vector3(k.x, 0f, k.y);
            _punch.position = hit;
            if (!Click.Down) return;

            float temp = Piece != null ? Piece.Temperature : 0f;
            _strikes.Add((hit, temp));
            _anvil?.SetPower(0.5f);
            _anvil?.Strike();
            FeedbackHub.Instance?.Hit(hit, Mathf.RoundToInt(40 + 50 * HeatScore(temp) / 100f));
            // 자국: 쇠가 뜨거울수록 진하다. 작업물에 붙여 남긴다
            var mark = ActivityKit.Prim(PrimitiveType.Cylinder, Piece != null ? Piece.transform : Props, Vector3.zero, new Vector3(0.012f, 0.0006f, 0.012f),
                ActivityKit.Mat(Color.Lerp(new Color(0.35f, 0.33f, 0.32f), new Color(0.05f, 0.04f, 0.04f), HeatScore(temp) / 100f)), name: "Maker Mark");
            mark.transform.position = new Vector3(hit.x, TargetPos.y + 0.0004f, hit.z);
            if (_strikes.Count == 2) _end = 1.2f;
        }

        static float HeatScore(float temp) => Mathf.Clamp01((temp - 600f) / 200f) * 100f;

        void Done()
        {
            var a = _strikes[0].pos;
            var b = _strikes[1].pos;
            var target = TargetPos;
            float dist = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(target.x, target.z));
            float gap = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            float pos = ActivityKit.Lin(dist, 3f * Unit, 18f * Unit) * 100f;
            float match = ActivityKit.Lin(gap, 2f * Unit, 14f * Unit) * 100f;
            float heat = (HeatScore(_strikes[0].temp) + HeatScore(_strikes[1].temp)) / 2f;
            int score = Scoring.Round(0.4f * pos + 0.3f * match + 0.3f * heat);
            Finish(new MinigameResult { Score = score }
                .Add("dist", "자리 어긋남(mm)", Mathf.Round(dist * 10000f) / 10f)
                .Add("gap", "두 자국 사이(mm)", Mathf.Round(gap * 10000f) / 10f)
                .Add("heatPct", "찍을 때 열(%)", Scoring.Round(heat)));
        }
    }
}
