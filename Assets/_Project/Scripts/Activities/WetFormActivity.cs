using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Minigames;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 칼집 ① 가죽 성형: 칼에 감은 젖은 가죽을 내려다본다. 마우스를 누른 채 칼 윤곽선(점선) 위를 문지르면 그 자리가 반들반들 잡힌다.
    // 가죽은 24초 동안 점점 말라 밝아지고, 다 마르면 끝. 점수 = 70% 잡힌 윤곽 비율 + 30% 윤곽선 위를 문지른 정확도.
    // 수치는 웹 WetFormStep.jsx 를 m 로 옮겼다.
    public sealed class WetFormActivity : ActivityBase
    {
        const float Dry = 24f, Done_ = 0.96f;
        const float Unit = SheathShape.Length / 196f;
        const float Near = 7f * Unit, Reach = 9f * Unit;
        const int PerSide = 22;

        Transform _sheath, _rubber;
        Material _leather;
        readonly List<(Vector3 local, Renderer r, bool formed)> _pts = new();
        Plane _plane;
        float _onLine, _rubT;

        public WetFormActivity(PartRef part) : base(part) { }

        float Formed => _pts.Count(p => p.formed) / (float)_pts.Count;

        public override string Hint => $"누른 채 칼 윤곽선(점선)을 따라 문지르기 · 잡힌 윤곽 {Scoring.Round(Formed * 100)}% · 가죽이 마르기까지 {Mathf.CeilToInt(Mathf.Max(0f, Dry - T))}초";

        protected override void Build()
        {
            var w = Station.WorkPoint.position;
            var right = -Vector3.Cross(Vector3.up, ToStand);
            _sheath = ActivityKit.Root("Sheath", Props, new Vector3(w.x, 0.89f + 0.006f, w.z) + ToStand * 0.08f, Quaternion.LookRotation(Vector3.Cross(right, Vector3.up)));
            _plane = new Plane(Vector3.up, _sheath.position + Vector3.up * 0.005f);
            _leather = ActivityKit.OwnMat(new Color(0.22f, 0.12f, 0.07f));
            SheathShape.Build(_sheath, _leather);
            var dot = ActivityKit.Mat(new Color(0.95f, 0.85f, 0.6f));
            foreach (int side in new[] { 1, -1 })
                for (int i = 0; i <= PerSide; i++)
                {
                    float u = i / (float)PerSide;
                    var local = new Vector3(SheathShape.XOf(u), 0.0045f, side * SheathShape.HalfWidth(u));
                    var g = ActivityKit.Prim(PrimitiveType.Cube, _sheath, local, new Vector3(0.004f, 0.001f, 0.004f), dot);
                    _pts.Add((local, g.GetComponent<Renderer>(), false));
                }
            _rubber = ActivityKit.Prim(PrimitiveType.Sphere, Props, Vector3.zero, new Vector3(0.025f, 0.012f, 0.025f), ActivityKit.Mat(new Color(0.75f, 0.7f, 0.6f)), name: "Burnisher").transform;
            Cam = ActivityKit.View(Station.transform, _sheath.position + ToStand * 0.18f + Vector3.up * 0.42f, _sheath.position, 34f);
        }

        protected override void Tick()
        {
            // 마를수록 밝은 가죽색
            _leather.SetColor("_BaseColor", Color.Lerp(new Color(0.22f, 0.12f, 0.07f), new Color(0.55f, 0.38f, 0.24f), T / Dry));
            if (ActivityKit.Aim(_plane, out var p)) _rubber.position = p + Vector3.up * 0.006f;
            if (Click.Held && T < Dry)
            {
                var local = _sheath.InverseTransformPoint(p);
                local.y = 0.0045f;
                float nearest = _pts.Min(x => Vector3.Distance(x.local, local));
                _rubT += Time.deltaTime;
                if (nearest <= Near)
                {
                    _onLine += Time.deltaTime;
                    var shaped = ActivityKit.Mat(new Color(0.15f, 0.08f, 0.05f), new Color(0.3f, 0.18f, 0.05f) * 0.4f);
                    for (int i = 0; i < _pts.Count; i++)
                        if (!_pts[i].formed && Vector3.Distance(_pts[i].local, local) <= Reach)
                        {
                            _pts[i].r.sharedMaterial = shaped;
                            _pts[i] = (_pts[i].local, _pts[i].r, true);
                        }
                }
            }
            if (Formed >= Done_ || T >= Dry) Done();
        }

        void Done()
        {
            float acc = _rubT > 0f ? _onLine / _rubT : 0f;
            Finish(new MinigameResult { Score = Scoring.Round(100f * (0.7f * Formed + 0.3f * acc)) }
                .Add("formedPct", "잡힌 윤곽(%)", Scoring.Round(Formed * 100f))
                .Add("accPct", "윤곽선 위를 문지른 비율(%)", Scoring.Round(acc * 100f))
                .Add("seconds", "걸린 시간(초)", Scoring.Round(Mathf.Min(T, Dry))));
        }
    }
}
