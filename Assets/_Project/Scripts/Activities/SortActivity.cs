using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 정련 ② 재질 분류: 모루 위에 부러뜨린 조각이 하나씩 놓이고, 단면이 대장장이 쪽을 본다.
    // 강(날감)은 곧게 부러지고 단면에 작은 반짝임이 고르게, 연철(몸통감)은 휘어 찢어지고 섬유 같은 긴 줄 · 검은 슬래그 줄.
    // A/← 또는 좌클릭 = 강, D/→ 또는 우클릭 = 연철. 조각마다 4초. 뒤로 갈수록 단서가 흐려진다. 수치는 웹 SortStep.jsx 와 같다.
    public sealed class SortActivity : ActivityBase
    {
        const int Pieces = 8;
        const float Limit = 4f;
        const float ShowSeconds = 0.7f;

        struct Rec { public bool Ok, Picked; public float T; public int Score; }

        readonly List<bool> _steel = new();
        readonly List<Rec> _recs = new();
        Transform _piece, _timer;
        Press _right;
        int _idx;
        float _t, _show;

        public SortActivity(PartRef part) : base(part) { }

        public override string Hint =>
            $"단면을 보고 고르기 · A/←/좌클릭 = 강(날감: 곧은 끝 · 고른 반짝임) · D/→/우클릭 = 연철(몸통감: 휜 끝 · 긴 섬유 줄) · {Mathf.Min(_idx + 1, Pieces)}/{Pieces}";

        protected override void Build()
        {
            for (int i = 0; i < Pieces; i++) _steel.Add(i % 2 == 0);
            for (int i = Pieces - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (_steel[i], _steel[j]) = (_steel[j], _steel[i]);
            }
            // 모루 앞 가장자리에 단면이 대장장이 쪽을 보게 놓는다
            Props.SetPositionAndRotation(Station.WorkPoint.position + Vector3.up * 0.04f + ToStand * 0.1f, FacingStand);
            // 남은 시간 막대: 조각 아래 빛나는 띠가 줄어든다
            _timer = ActivityKit.Prim(PrimitiveType.Cube, Props, new Vector3(0f, -0.045f, 0.02f), new Vector3(0.2f, 0.006f, 0.01f),
                ActivityKit.Mat(new Color(1f, 0.7f, 0.2f), new Color(1f, 0.6f, 0.15f) * 1.5f)).transform;
            Cam = ActivityKit.View(Station.transform, Props.position + ToStand * 0.3f + Vector3.up * 0.1f, Props.position, 32f);
            ShowPiece();
        }

        protected override void OnEnter()
        {
            _right.Reset();
            (Station as AnvilStation)?.SetPower(0.85f);   // 망치를 들어 조각을 가리지 않게
        }

        protected override void OnExit() => (Station as AnvilStation)?.SetPower(0f);

        void ShowPiece()
        {
            if (_piece != null) Object.Destroy(_piece.gameObject);
            if (_idx >= Pieces) return;
            bool steel = _steel[_idx];
            float cue = 1f - 0.55f * (_idx / (float)(Pieces - 1));
            float fine = steel ? cue : (1f - cue) * 0.6f;
            float fiber = steel ? (1f - cue) * 0.6f : cue;

            _piece = new GameObject("Piece").transform;
            _piece.SetParent(Props, false);
            // 연철일수록 끝이 휜다
            _piece.localRotation = Quaternion.Euler(fiber * 12f, fiber * 18f, 0f);
            var iron = ActivityKit.Mat(new Color(0.42f, 0.41f, 0.4f));
            ActivityKit.Prim(PrimitiveType.Cube, _piece, new Vector3(0f, 0f, -0.12f), new Vector3(0.07f, 0.06f, 0.24f), iron);
            // 단면(대장장이 쪽을 보는 면)
            var faceColor = Color.Lerp(new Color(0.55f, 0.54f, 0.53f), steel ? new Color(0.68f, 0.7f, 0.74f) : new Color(0.45f, 0.43f, 0.41f), cue);
            ActivityKit.Prim(PrimitiveType.Cube, _piece, new Vector3(0f, 0f, 0.001f), new Vector3(0.068f, 0.058f, 0.004f), ActivityKit.Mat(faceColor));
            var r = new System.Random(Rng.Next());
            var sparkle = ActivityKit.Mat(Color.white, Color.white * 1.6f);
            int dots = Mathf.RoundToInt(45 * fine);
            for (int i = 0; i < dots; i++)
                ActivityKit.Prim(PrimitiveType.Cube, _piece, new Vector3(((float)r.NextDouble() - 0.5f) * 0.06f, ((float)r.NextDouble() - 0.5f) * 0.05f, 0.0035f),
                    Vector3.one * (0.0018f + 0.0015f * (float)r.NextDouble()), sparkle);
            var light = ActivityKit.Mat(new Color(0.85f, 0.84f, 0.82f));
            var slag = ActivityKit.Mat(new Color(0.08f, 0.07f, 0.07f));
            int streaks = Mathf.RoundToInt(12 * fiber);
            for (int i = 0; i < streaks; i++)
            {
                bool dark = r.NextDouble() > 0.7;
                float len = 0.02f + 0.04f * (float)r.NextDouble();
                ActivityKit.Prim(PrimitiveType.Cube, _piece, new Vector3(-0.03f + len / 2f + 0.01f * (float)r.NextDouble(), ((float)r.NextDouble() - 0.5f) * 0.05f, 0.0035f),
                    new Vector3(len, dark ? 0.003f : 0.0018f, 0.001f), dark ? slag : light);
            }
            _t = 0f;
        }

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            _right.Update(ActivityKit.RightHeld);
            if (_show > 0f)
            {
                _show -= dt;
                if (_show <= 0f)
                {
                    _idx++;
                    if (_idx >= Pieces)
                    {
                        Done();
                        return;
                    }
                    ShowPiece();
                }
                return;
            }
            _t += dt;
            _timer.localScale = new Vector3(0.2f * Mathf.Max(0f, 1f - _t / Limit), 0.006f, 0.01f);

            bool steel = Click.Down || ActivityKit.KeyDown(Key.A) || ActivityKit.KeyDown(Key.LeftArrow);
            bool iron = _right.Down || ActivityKit.KeyDown(Key.D) || ActivityKit.KeyDown(Key.RightArrow);
            if (steel || iron)
            {
                bool ok = _steel[_idx] == steel;
                _recs.Add(new Rec { Ok = ok, Picked = true, T = _t, Score = ok ? Scoring.Round(100 - 25 * (_t / Limit)) : 0 });
                FeedbackHub.Instance?.Hit(_piece.position, ok ? 80 : 10);
                Toast(ok ? $"맞았습니다! {(_steel[_idx] ? "강" : "연철")}" : $"틀렸습니다 — {(_steel[_idx] ? "강" : "연철")}");
                _show = ShowSeconds;
            }
            else if (_t >= Limit)
            {
                _recs.Add(new Rec { T = Limit });
                Toast($"시간 초과 — {(_steel[_idx] ? "강" : "연철")}");
                _show = ShowSeconds;
            }
        }

        void Done()
        {
            var answered = _recs.Where(x => x.Ok).ToList();
            Finish(new MinigameResult { Score = Scoring.Round(_recs.Average(x => x.Score)) }
                .Add("correct", "맞힘", answered.Count)
                .Add("wrong", "틀림", _recs.Count(x => x.Picked && !x.Ok))
                .Add("missed", "놓침", _recs.Count(x => !x.Picked))
                .Add("avgSeconds", "맞힌 평균 시간(초)", answered.Count > 0 ? (Mathf.Round(answered.Average(x => x.T) * 10f) / 10f).ToString() : "-"));
        }
    }
}
