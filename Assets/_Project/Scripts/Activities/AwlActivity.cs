using System.Collections.Generic;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 손잡이 ① 그립 구멍: 작업대에 눕힌 통짜 그립을 내려다본다. 누르고 있으면 송곳이 먼 쪽 끝을 향해 파고들고,
    // 송곳 끝이 저절로 옆으로 흐르므로 마우스를 반대로 움직여 금빛 가운데 선을 지킨다. 지나간 자리에 구멍 자국이 남는다.
    // 점수 = 가운데를 지킨 정도(깊이로 가중). 수치는 웹 AwlStep.jsx 를 m 로 옮겼다(그립 0.14m = 174칸).
    public sealed class AwlActivity : ActivityBase
    {
        const float Unit = 0.14f / 174f;
        const float Speed = 0.1f;   // 깊이/초, 끝까지 약 10초
        const float Len = 0.14f;

        Transform _grip, _awl;
        Plane _plane;
        float _depth, _phase, _sumAcc, _sumW, _sumErr, _maxErr, _lastTrail = -1f, _end, _mouseX;
        int _n;

        public AwlActivity(PartRef part) : base(part) { }

        public override string Hint => $"누르고 있으면 파고듭니다 · 송곳 끝이 옆으로 흐르니 마우스를 반대로 움직여 금빛 선을 지키세요 · 깊이 {Scoring.Round(_depth * 100)}%";

        float Drift => (0.7f + 0.6f * _depth) * (13f * Mathf.Sin(T * 0.85f + _phase) + 7f * Mathf.Sin(T * 2.1f + _phase * 1.7f)) * Unit;

        protected override void Build()
        {
            _phase = Rand * 10f;
            var w = Station.WorkPoint.position;
            // 그립: 길이 방향이 대장장이에게서 멀어지는 쪽(+z = 먼 쪽 끝)
            _grip = ActivityKit.Root("Grip", Props, new Vector3(w.x, 0.89f + 0.02f, w.z), Quaternion.LookRotation(-ToStand));
            _plane = new Plane(Vector3.up, _grip.position + Vector3.up * 0.02f);
            ActivityKit.Prim(PrimitiveType.Cube, _grip, new Vector3(0f, 0f, Len / 2f), new Vector3(0.05f, 0.04f, Len), ActivityKit.Mat(new Color(0.55f, 0.36f, 0.2f)));
            ActivityKit.Prim(PrimitiveType.Cube, _grip, new Vector3(0f, 0.0205f, Len / 2f), new Vector3(0.0015f, 0.001f, Len), ActivityKit.Mat(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.7f, 0.2f) * 1.6f));
            _awl = new GameObject("Awl").transform;
            _awl.SetParent(_grip, false);
            ActivityKit.Prim(PrimitiveType.Cylinder, _awl, new Vector3(0f, 0.05f, 0f), new Vector3(0.006f, 0.03f, 0.006f), ActivityKit.Mat(new Color(0.6f, 0.62f, 0.66f)));
            ActivityKit.Prim(PrimitiveType.Cylinder, _awl, new Vector3(0f, 0.1f, 0f), new Vector3(0.022f, 0.025f, 0.022f), ActivityKit.Mat(new Color(0.4f, 0.25f, 0.14f)));
            Cam = ActivityKit.View(Station.transform, _grip.position + ToStand * 0.18f + Vector3.up * 0.32f, _grip.position + _grip.forward * 0.07f, 34f);
        }

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            if (_end > 0f)
            {
                _end -= dt;
                if (_end <= 0f) Done();
                return;
            }
            if (ActivityKit.Aim(_plane, out var p)) _mouseX = _grip.InverseTransformPoint(p).x;
            float tip = _mouseX + Drift;
            _awl.localPosition = new Vector3(tip, 0.02f, _depth * Len);
            if (!(Click.Held || Space.Held)) return;

            float step = Mathf.Min(Speed * dt, 1f - _depth);
            float err = Mathf.Abs(tip);
            _depth += step;
            _sumAcc += ActivityKit.Lin(err, 3f * Unit, 15f * Unit) * step;
            _sumW += step;
            _sumErr += err;
            _n++;
            _maxErr = Mathf.Max(_maxErr, err);
            if (_depth - _lastTrail >= 0.012f)
            {
                _lastTrail = _depth;
                ActivityKit.Prim(PrimitiveType.Cube, _grip, new Vector3(tip, 0.0205f, _depth * Len), new Vector3(0.004f, 0.001f, 0.003f), ActivityKit.Mat(new Color(0.12f, 0.07f, 0.04f)));
            }
            if (_depth >= 1f)
            {
                _end = 1f;
                FeedbackHub.Instance?.Poke(_awl.position);
            }
        }

        void Done()
        {
            float acc = _sumW > 0f ? _sumAcc / _sumW : 0f;
            Finish(new MinigameResult { Score = Scoring.Round(acc * 100f) }
                .Add("accPct", "가운데를 지킨 정도(%)", Scoring.Round(acc * 100f))
                .Add("avgErr", "평균 벗어남(mm)", Mathf.Round(_sumErr / Mathf.Max(1, _n) * 10000f) / 10f)
                .Add("maxErr", "가장 크게 벗어남(mm)", Mathf.Round(_maxErr * 10000f) / 10f));
        }
    }
}
