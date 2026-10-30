using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Pieces;
using Blacksmith.Stations;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 칼날 단조 ① 형상: 모루 위 철 바의 날 가장자리에 빛나는 표적이 하나씩 뜬다. 떨리는 조준 고리를 표적에 맞춰 클릭하면
    // 망치가 내리친다. 4라운드 × 6표적, 표적마다 3.6초, 라운드마다 떨림이 커진다. 칠수록 철 바가 얇고 길게 펴진다.
    // 쇠가 체리빛(750℃) 아래면 반, 600℃ 아래면 0.2배 — 식으면 화덕에 다녀온다. 수치는 웹 ShapeStage.jsx 를 m 로 옮겼다.
    public sealed class FormActivity : ActivityBase
    {
        const int Rounds = 4, PerRound = 6;
        const float Limit = 3.6f;
        const float Unit = 0.4f / 206f;              // 웹 viewBox 한 칸 = m
        static readonly float[] Jitter = { 4f * Unit, 6f * Unit, 8f * Unit, 11f * Unit };
        const float Snap = 8f * Unit, Miss = 26f * Unit;

        struct Rec { public int Score; public float Dist; public bool Cold, Timeout; }

        AnvilStation _anvil;
        Transform _target, _reticle;
        readonly List<Rec> _recs = new();
        Vector3 _aim, _targetLocal;
        float _t;
        Plane _plane;

        public FormActivity(PartRef part) : base(part) { }

        int Index => _recs.Count;
        float HeatFactor => Piece == null ? 1f : Piece.Temperature >= Workpiece.WorkableTemp ? 1f : Piece.Temperature >= 600f ? 0.5f : 0.2f;

        public override string Hint
        {
            get
            {
                string cold = HeatFactor < 1f ? " · 쇠가 식었습니다! 화덕에서 달궈 오세요" : "";
                return $"떨리는 고리를 빛나는 표적에 맞춰 클릭 · {Mathf.Min(Index / PerRound + 1, Rounds)}라운드 {Index % PerRound + 1}/{PerRound}{cold}";
            }
        }

        protected override void Build()
        {
            _anvil = Station as AnvilStation;
            var w = Station.WorkPoint.position;
            _plane = new Plane(Vector3.up, w + Vector3.up * 0.03f);
            var glow = ActivityKit.Mat(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.7f, 0.2f) * 2.5f);
            _target = ActivityKit.Prim(PrimitiveType.Cylinder, Props, Vector3.zero, new Vector3(0.022f, 0.002f, 0.022f), glow, name: "Target").transform;
            _reticle = new GameObject("Reticle").transform;
            _reticle.SetParent(Props, false);
            var white = ActivityKit.Mat(Color.white, Color.white * 1.5f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                ActivityKit.Prim(PrimitiveType.Cube, _reticle, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.016f), new Vector3(0.003f, 0.002f, 0.012f), white, Quaternion.Euler(0f, a, 0f));
            }
            ActivityKit.Prim(PrimitiveType.Cube, _reticle, Vector3.zero, Vector3.one * 0.003f, white);
            MakeView(0.5f, 0.55f, Vector3.zero, 32f);
            NextTarget();
        }

        protected override void OnEnter() => _anvil?.SetPower(0.85f);   // 망치를 들어 칼날 위를 비운다
        protected override void OnExit() => _anvil?.SetPower(0f);

        void NextTarget()
        {
            // 날 가장자리(양쪽 번갈아), 길이 방향은 무작위
            float x = Mathf.Lerp(-0.16f, 0.16f, Rand);
            float z = (Index % 2 == 0 ? 1f : -1f) * 0.028f;
            _targetLocal = new Vector3(x, 0.03f, z);
            _target.position = Station.WorkPoint.position + _targetLocal;
            _t = 0f;
        }

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _anvil?.SetPower(0.85f);   // 내리친 뒤 다시 든다
            float jitter = Jitter[Mathf.Min(Index / PerRound, Rounds - 1)] * Mod("steady", 1f);
            if (ActivityKit.Aim(_plane, out var p)) _aim = p;
            var k = ActivityKit.Tremor(T, jitter);
            var hit = _aim + new Vector3(k.x, 0f, k.y);
            _reticle.position = hit + Vector3.up * 0.002f;
            float shrink = 1f - 0.6f * (_t / Limit);
            _target.localScale = new Vector3(0.022f * shrink, 0.002f, 0.022f * shrink);

            if (Click.Down) Strike(hit);
            else if (_t >= Limit) Record(new Rec { Timeout = true, Dist = Miss });
        }

        void Strike(Vector3 hit)
        {
            var t = _target.position;
            float d = Vector2.Distance(new Vector2(hit.x, hit.z), new Vector2(t.x, t.z));
            int sc = d <= Snap ? 100 : d >= Miss ? 0 : Scoring.Round((Miss - d) / (Miss - Snap) * 100);
            float f = HeatFactor;
            sc = Scoring.Round(sc * f);
            _anvil?.SetPower(0.6f);
            _anvil?.Strike();
            FeedbackHub.Instance?.Hit(hit, f < 1f ? Mathf.Min(sc, 20) : sc);
            Record(new Rec { Score = sc, Dist = d, Cold = f < 1f });
        }

        void Record(Rec r)
        {
            _recs.Add(r);
            // 칠수록 철 바가 길고 얇게 펴진다
            if (Piece != null)
            {
                float k = _recs.Count / (float)(Rounds * PerRound);
                Piece.transform.localScale = new Vector3(1f + 0.15f * k, 1f - 0.45f * k, 1f - 0.15f * k);
            }
            if (_recs.Count >= Rounds * PerRound)
            {
                Done();
                return;
            }
            NextTarget();
            if (r.Timeout) Toast("놓쳤습니다");
        }

        void Done()
        {
            if (Piece != null) Piece.transform.localScale = Vector3.one;
            var rounds = Enumerable.Range(0, Rounds).Select(i => Scoring.Round(_recs.Skip(i * PerRound).Take(PerRound).Average(x => x.Score)));
            Finish(new MinigameResult { Score = Scoring.Round(_recs.Average(x => x.Score)) }
                .Add("hitRate", "명중률(%)", Scoring.Round(_recs.Count(x => x.Score >= 60) * 100.0 / _recs.Count))
                .Add("avgDist", "평균 빗나간 거리(mm)", Mathf.Round(_recs.Where(x => !x.Timeout).DefaultIfEmpty(new Rec { Dist = Miss }).Average(x => x.Dist) * 1000f))
                .Add("timeouts", "놓친 표적", _recs.Count(x => x.Timeout))
                .Add("coldHits", "식은 채로 친 횟수", _recs.Count(x => x.Cold))
                .Add("perRound", "라운드별 평균", string.Join(" / ", rounds)));
        }
    }
}
