using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Stations;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 손잡이 ③ 리벳: 폼멜 위로 튀어나온 슴베 끝을 망치로 두드려 퍼뜨린다. 접기 단접과 같은 망치질(누르고 있다가 떼기,
    // 모루 옆 눈금 기둥의 금색 띠가 필요한 힘). 5번, 힘이 1.1초에 가득 차고 넘기면 다시 빠진다. 수치는 웹 HandleStage.jsx 와 같다.
    public sealed class RivetActivity : ActivityBase
    {
        const int Rivets = 5;
        const float FillSeconds = 1.1f;
        const float Half = 0.1f;

        AnvilStation _anvil;
        Transform _head;
        readonly List<int> _scores = new();
        float _ct, _pos, _target;
        bool _charging;

        public RivetActivity(PartRef part) : base(part) { }

        public override string Hint => $"누르고 있다가 떼서 내리치기 (스페이스도 됨) · 망치 높이를 기둥의 금색 띠에 · 너무 세면 슴베가 휩니다 · {_scores.Count}/{Rivets}";

        protected override void Build()
        {
            _anvil = Station as AnvilStation;
            _target = 0.5f + Rand * 0.3f;
            // 폼멜 끝에 튀어나온 슴베 머리: 칠수록 납작하게 퍼진다
            _head = ActivityKit.Prim(PrimitiveType.Cylinder, Props, Vector3.zero, new Vector3(0.012f, 0.012f, 0.012f),
                ActivityKit.Mat(new Color(0.6f, 0.6f, 0.63f)), Quaternion.Euler(0f, 0f, 90f), "Tang Head").transform;
            _head.position = Station.WorkPoint.position + new Vector3(-0.21f, 0.01f, 0f);
            MakeView(0.6f, 0.45f, new Vector3(-0.05f, 0f, 0f), 38f);
        }

        protected override void OnEnter()
        {
            _charging = false;
            _anvil?.ShowTarget(_target, Half);
        }

        protected override void OnExit()
        {
            _charging = false;
            _anvil?.SetPower(0f);
            _anvil?.SetGaugeVisible(false);
        }

        protected override void Tick()
        {
            if (_anvil == null) return;
            bool held = Click.Held || Space.Held;
            if ((Click.Down || Space.Down) && !_charging)
            {
                _charging = true;
                _ct = 0f;
            }
            if (!_charging) return;
            _ct += Time.deltaTime;
            _pos = Gauge.Triangle(_ct, 2f * FillSeconds);
            _anvil.SetPower(_pos);
            if (held) return;

            _charging = false;
            int sc = Gauge.Score(_pos, _target, Half);
            _scores.Add(sc);
            _anvil.Strike();
            FeedbackHub.Instance?.Hit(_head.position, sc);
            Toast(Gauge.HitWord(sc));
            float spread = 1f + 0.25f * _scores.Count(v => v >= 60);
            _head.localScale = new Vector3(0.012f * spread, 0.012f / spread, 0.012f * spread);
            if (_scores.Count >= Rivets)
            {
                Finish(new MinigameResult { Score = Scoring.Round(_scores.Average()) }
                    .Add("hits", "타격 점수", string.Join(" / ", _scores))
                    .Add("good", "제대로 맞음", _scores.Count(v => v >= 60)));
                return;
            }
            _target = 0.5f + Rand * 0.3f;
            _anvil.ShowTarget(_target, Half);
        }
    }
}
