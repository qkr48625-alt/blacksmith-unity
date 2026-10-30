using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 칼집 ② 도장 꾸미기: 젖은 가죽 칼집 앞면을 내려다보고, 클릭한 자리에 도장을 찍는다(4~10개). Enter 로 마친다(40초 제한).
    // 점수 = 좌우 대칭 45% + 고른 간격 40% + 길이 방향으로 퍼진 정도 15%, 6개보다 적으면 그만큼 깎인다.
    // 가운데 금빛 선이 대칭 기준. 수치는 웹 StampDesignStep.jsx 를 m 로 옮겼다.
    public sealed class StampActivity : ActivityBase
    {
        const float Limit = 40f, Edge = 0.82f;
        const int Max = 10, Min = 4, GoodCount = 6;
        const float Unit = SheathShape.Length / 196f;
        const float MinGap = 12f * Unit;

        Transform _sheath, _stamp;
        readonly List<Vector2> _stamps = new();   // (x = 길이, z = 폭) 로컬
        Plane _plane;
        float _end;

        public StampActivity(PartRef part) : base(part) { }

        public override string Hint =>
            $"클릭해서 도장 찍기 · 금빛 선을 기준으로 좌우 대칭 · 고른 간격 · {_stamps.Count}/{Max}개{(_stamps.Count >= Min ? " · Enter 로 마치기" : $" · {Min}개 이상")} · 남은 {Mathf.CeilToInt(Mathf.Max(0f, Limit - T))}초";

        protected override void Build()
        {
            var w = Station.WorkPoint.position;
            var right = -Vector3.Cross(Vector3.up, ToStand);
            _sheath = ActivityKit.Root("Sheath", Props, new Vector3(w.x, 0.89f + 0.006f, w.z) + ToStand * 0.08f, Quaternion.LookRotation(Vector3.Cross(right, Vector3.up)));
            _plane = new Plane(Vector3.up, _sheath.position + Vector3.up * 0.005f);
            SheathShape.Build(_sheath, ActivityKit.Mat(new Color(0.3f, 0.17f, 0.1f)));
            ActivityKit.Prim(PrimitiveType.Cube, _sheath, new Vector3(0f, 0.0045f, 0f), new Vector3(SheathShape.Length, 0.001f, 0.0012f), ActivityKit.Mat(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.7f, 0.2f) * 1.2f));
            _stamp = new GameObject("Stamp").transform;
            _stamp.SetParent(Props, false);
            ActivityKit.Prim(PrimitiveType.Cylinder, _stamp, new Vector3(0f, 0.035f, 0f), new Vector3(0.012f, 0.03f, 0.012f), ActivityKit.Mat(new Color(0.55f, 0.56f, 0.6f)));
            ActivityKit.Prim(PrimitiveType.Cylinder, _stamp, new Vector3(0f, 0.002f, 0f), new Vector3(0.01f, 0.002f, 0.01f), ActivityKit.Mat(Color.white, Color.white));
            Cam = ActivityKit.View(Station.transform, _sheath.position + ToStand * 0.18f + Vector3.up * 0.42f, _sheath.position, 34f);
        }

        protected override void Tick()
        {
            if (_end > 0f)
            {
                _end -= Time.deltaTime;
                if (_end <= 0f) Done();
                return;
            }
            if (ActivityKit.Aim(_plane, out var p)) _stamp.position = p;
            if (Click.Down && _stamps.Count < Max)
            {
                var local = _sheath.InverseTransformPoint(p);
                float u = SheathShape.UOf(local.x);
                bool inside = u >= 0.02f && u <= 0.95f && Mathf.Abs(local.z) <= SheathShape.HalfWidth(u) * Edge;
                bool overlap = _stamps.Any(s => Vector2.Distance(s, new Vector2(local.x, local.z)) < MinGap);
                if (!inside) Toast("가죽 앞면 안쪽에 찍으세요");
                else if (overlap) Toast("다른 도장과 겹칩니다");
                else
                {
                    _stamps.Add(new Vector2(local.x, local.z));
                    ActivityKit.Prim(PrimitiveType.Cylinder, _sheath, new Vector3(local.x, 0.0045f, local.z), new Vector3(0.009f, 0.0008f, 0.009f), ActivityKit.Mat(new Color(0.12f, 0.06f, 0.03f)));
                    FeedbackHub.Instance?.Poke(p);
                }
            }
            bool finish = (ActivityKit.KeyDown(Key.Enter) || ActivityKit.KeyDown(Key.NumpadEnter)) && _stamps.Count >= Min;
            if (finish || _stamps.Count >= Max || T >= Limit) _end = 0.6f;
        }

        void Done()
        {
            int n = _stamps.Count;
            float sym = 0f, even = 0f, spread = 0f;
            if (n > 0)
            {
                // 대칭: 점마다 가운데 선 건너편 거울 자리에서 가장 가까운 도장까지
                sym = ActivityKit.Lin(_stamps.Average(a => _stamps.Min(b => Vector2.Distance(new Vector2(a.x, -a.y), b))), 2f * Unit, 14f * Unit) * 100f;
                if (n >= 2)
                {
                    var nn = _stamps.Select((a, i) => _stamps.Where((_, j) => j != i).Min(b => Vector2.Distance(a, b))).ToList();
                    float mean = nn.Average();
                    float sd = Mathf.Sqrt(nn.Average(d => (d - mean) * (d - mean)));
                    even = ActivityKit.Lin(sd / mean, 0.1f, 0.55f) * 100f;
                }
                spread = Mathf.Min(1f, (_stamps.Max(s => s.x) - _stamps.Min(s => s.x)) / SheathShape.Length / 0.6f) * 100f;
            }
            int score = Scoring.Round((0.45f * sym + 0.4f * even + 0.15f * spread) * Mathf.Min(1f, n / (float)GoodCount));
            Finish(new MinigameResult { Score = score }
                .Add("count", "도장 수", n)
                .Add("symPct", "좌우 대칭(%)", Scoring.Round(sym))
                .Add("evenPct", "고른 간격(%)", Scoring.Round(even))
                .Add("spreadPct", "퍼진 정도(%)", Scoring.Round(spread)));
        }
    }
}
