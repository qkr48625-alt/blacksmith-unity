using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 칼집 ③ 리듬 바느질: 칼집 솔기를 따라 구멍 8개. 박자 구슬이 솔기 위를 미끄러져 와 금빛 고리에 닿을 때
    // 찌르기(좌클릭 / A, 붉은 구슬)와 당기기(우클릭 / D, 푸른 구슬)를 번갈아 누른다. 16박자, 간격 0.75초 → 0.48초로 빨라진다.
    // 한 박자 점수: 0.07초 안 100 · 0.15초 안 70 · 0.25초 안 30 · 그 밖 0. 수치는 웹 RhythmStitchStep.jsx 와 같다.
    public sealed class StitchActivity : ActivityBase
    {
        const int Holes = 8;
        const float Lead = 1.6f, StartGap = 0.75f, EndGap = 0.48f, Perfect = 0.07f, Good = 0.15f, Bad = 0.25f;
        const float StartDelay = 1.5f;

        struct Beat { public float Time; public bool Pull; public int Score; public bool Judged; public Transform Ball; }

        readonly List<Beat> _beats = new();
        Transform _sheath, _ring;
        Vector3 _judgeLocal;
        Press _right;
        float _clock, _end;
        int _stitched;

        public StitchActivity(PartRef part) : base(part) { }

        public override string Hint => $"구슬이 금빛 고리에 닿을 때 · 붉은 = 찌르기(좌클릭/A) · 푸른 = 당기기(우클릭/D) · {_beats.Count(b => b.Judged)}/{_beats.Count}박자";

        protected override void Build()
        {
            var w = Station.WorkPoint.position;
            var right = -Vector3.Cross(Vector3.up, ToStand);
            _sheath = ActivityKit.Root("Sheath", Props, new Vector3(w.x, 0.89f + 0.006f, w.z) + ToStand * 0.08f, Quaternion.LookRotation(Vector3.Cross(right, Vector3.up)));
            SheathShape.Build(_sheath, ActivityKit.Mat(new Color(0.3f, 0.17f, 0.1f)));
            // 솔기: 가까운 쪽 가장자리. 판정 고리는 솔기 왼쪽(입구) 끝
            _judgeLocal = new Vector3(SheathShape.XOf(0.08f), 0.012f, SheathShape.HalfWidth(0.08f) * 0.75f);
            _ring = new GameObject("Judge Ring").transform;
            _ring.SetParent(_sheath, false);
            _ring.localPosition = _judgeLocal;
            var gold = ActivityKit.Mat(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.7f, 0.2f) * 2f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f;
                ActivityKit.Prim(PrimitiveType.Cube, _ring, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.014f), new Vector3(0.006f, 0.002f, 0.002f), gold, Quaternion.Euler(0f, a, 0f));
            }
            // 박자표: 구멍 하나에 찌르기 → 당기기
            float t = StartDelay;
            for (int i = 0; i < Holes * 2; i++)
            {
                var red = ActivityKit.Mat(new Color(0.9f, 0.25f, 0.2f), new Color(1f, 0.2f, 0.1f) * 1.2f);
                var blue = ActivityKit.Mat(new Color(0.25f, 0.45f, 0.95f), new Color(0.2f, 0.4f, 1f) * 1.2f);
                bool pull = i % 2 == 1;
                var ball = ActivityKit.Prim(PrimitiveType.Sphere, _sheath, Vector3.zero, Vector3.one * 0.012f, pull ? blue : red).transform;
                ball.gameObject.SetActive(false);
                _beats.Add(new Beat { Time = t, Pull = pull, Ball = ball });
                t += Mathf.Lerp(StartGap, EndGap, i / (float)(Holes * 2 - 1));
            }
            Cam = ActivityKit.View(Station.transform, _sheath.position + ToStand * 0.2f + Vector3.up * 0.4f, _sheath.position, 34f);
        }

        protected override void OnEnter() => _right.Reset();

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            _right.Update(ActivityKit.RightHeld);
            if (_end > 0f)
            {
                _end -= dt;
                if (_end <= 0f) Done();
                return;
            }
            _clock += dt;
            // 구슬: 판정 시각 Lead 초 전에 솔기 끝(칼집 끝 쪽)에서 나와 고리로 미끄러져 온다
            var far = new Vector3(SheathShape.XOf(0.9f), 0.012f, SheathShape.HalfWidth(0.9f) * 0.75f);
            for (int i = 0; i < _beats.Count; i++)
            {
                var b = _beats[i];
                float k = 1f - (b.Time - _clock) / Lead;
                bool show = !b.Judged && k >= 0f && k <= 1.15f;
                b.Ball.gameObject.SetActive(show);
                if (show) b.Ball.localPosition = Vector3.LerpUnclamped(far, _judgeLocal, k);
                if (!b.Judged && _clock - b.Time > Bad) Judge(i, 0);
            }
            bool stab = Click.Down || ActivityKit.KeyDown(Key.A);
            bool pull = _right.Down || ActivityKit.KeyDown(Key.D);
            if (stab) Strike(false);
            if (pull) Strike(true);
            if (_beats.All(b => b.Judged)) _end = 0.8f;
        }

        void Strike(bool pull)
        {
            // 아직 판정 안 된 가장 이른 박자 중 같은 손
            int idx = _beats.FindIndex(b => !b.Judged && b.Pull == pull && Mathf.Abs(b.Time - _clock) <= Bad);
            if (idx < 0) return;
            float off = Mathf.Abs(_beats[idx].Time - _clock);
            Judge(idx, off <= Perfect ? 100 : off <= Good ? 70 : 30);
        }

        void Judge(int i, int score)
        {
            var b = _beats[i];
            b.Judged = true;
            b.Score = score;
            b.Ball.gameObject.SetActive(false);
            _beats[i] = b;
            if (score > 0)
            {
                FeedbackHub.Instance?.Poke(_ring.position);
                // 당기기까지 맞으면 솔기에 바늘땀이 하나 남는다
                if (b.Pull)
                {
                    float u = 0.12f + 0.78f * (_stitched / (float)(Holes - 1));
                    ActivityKit.Prim(PrimitiveType.Cube, _sheath, new Vector3(SheathShape.XOf(u), 0.0045f, SheathShape.HalfWidth(u) * 0.8f), new Vector3(0.008f, 0.001f, 0.002f), ActivityKit.Mat(new Color(0.9f, 0.85f, 0.7f)));
                    _stitched++;
                }
            }
            if (score == 0) FeedbackHub.Instance?.Hit(_ring.position, 0);
        }

        void Done()
        {
            Finish(new MinigameResult { Score = Scoring.Round(_beats.Average(b => b.Score)) }
                .Add("perfect", "완벽", _beats.Count(b => b.Score >= 100))
                .Add("good", "좋아요", _beats.Count(b => b.Score == 70))
                .Add("miss", "놓침", _beats.Count(b => b.Score == 0)));
        }
    }
}
