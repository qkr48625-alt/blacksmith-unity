using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Stations;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 제련 ① 풀무질: 광석을 숯 속에 넣고 30초 동안 불을 온도계의 금색 칸(폭 100℃)에 지킨다.
    // 금색 칸은 몇 초마다 다른 온도로 옮겨 가서, 바람을 더 넣거나 멈춰 따라가야 한다. 옮겨 가기 1초 전에 "띵" 하고 알린다.
    // 옮겨 간 직후 잠깐(옮긴 거리만큼)은 따라가는 시간이라, 그동안 칸 밖에 있던 시간은 점수 계산에서 뺀다. 머문 시간 비율이 점수.
    // 불 온도 · 풀무 조작은 ForgeStation 이 맡고, 여기서는 시간 · 금색 칸 · 측정값만 다룬다(화덕을 떠나 있는 동안은 멈춘다).
    public sealed class BellowsActivity : IStationActivity
    {
        const float Seconds = 30f;
        float HalfBand = 50f;   // 큰 풀무 업그레이드면 70
        const float FirstCenter = 1150f;
        const float CenterMin = 950f;
        const float CenterMax = 1300f;
        // 새 칸은 지금 칸에서 100~200℃ 떨어진 곳(더 멀면 불을 끌어올리는 데만 몇 초가 걸려 따라갈 수 없다)
        const float MinJump = 100f, MaxJump = 200f;
        const float HoldMin = 4.5f, HoldMax = 7f;
        const float WarnSeconds = 1f;
        const float GracePerDegree = 1f / 80f;   // 옮겨 간 거리 80℃ 마다 1초를 따라가는 시간으로 준다

        ForgeStation _forge;
        System.Random _rng;
        float _t, _inBand, _counted, _sum, _pumpT, _min = float.MaxValue, _max;
        float _center = FirstCenter, _next, _nextShift, _graceT;
        bool _warned;
        int _shifts;

        public BellowsActivity(PartRef part) => Part = part;

        public PartRef Part { get; }
        public bool IsDone { get; private set; }
        public MinigameResult Result { get; private set; }

        public string Hint
        {
            get
            {
                float left = Mathf.Max(0f, Seconds - _t);
                string state = _forge == null ? "" :
                    _forge.FireTemp > _center + HalfBand ? "너무 뜨겁습니다 — 손을 떼세요" :
                    _forge.FireTemp >= _center - HalfBand ? "좋습니다, 유지하세요" : "더 달궈야 합니다 — 풀무질!";
                string soon = _warned ? " · 금색 칸이 곧 옮겨 갑니다!" : "";
                return $"마우스를 누른 채 아래로 끌어 풀무질 (스페이스도 됨) · 바늘을 금색 칸에 · {state}{soon} · 남은 {Mathf.CeilToInt(left)}초";
            }
        }

        public void Begin(ActivityContext ctx)
        {
            _forge = ctx.Station as ForgeStation;
            _rng = ctx.Game.Rng;
            HalfBand = ctx.Game.Mod("bandHalf", 50f);
            _nextShift = Hold();
            _next = PickNext();
            _graceT = 3f;   // 처음 불을 끌어올리는 시간
            if (_forge == null) Debug.LogError("[Bellows] 화덕이 아닌 작업대에서 시작했습니다.");
        }

        float Hold() => HoldMin + (float)_rng.NextDouble() * (HoldMax - HoldMin);

        float PickNext()
        {
            float jump = Mathf.Lerp(MinJump, MaxJump, (float)_rng.NextDouble());
            bool up = _rng.NextDouble() < 0.5;
            // 끝에 닿으면 반대쪽으로
            if (_center + jump > CenterMax) up = false;
            if (_center - jump < CenterMin) up = true;
            return Mathf.Round(Mathf.Clamp(_center + (up ? jump : -jump), CenterMin, CenterMax) / 10f) * 10f;
        }

        public void Enter()
        {
            _forge?.ShowOre(true);
            _forge?.SetBand(_center, HalfBand);
        }

        public void Exit() => _forge?.ResetBand();

        public void Tick(float dt)
        {
            if (IsDone || _forge == null) return;
            _t += dt;

            // 금색 칸 옮기기: 1초 전에 알리고, 때가 되면 옮긴다
            if (!_warned && _t >= _nextShift - WarnSeconds && _nextShift < Seconds - 2f)
            {
                _warned = true;
                _forge.BlinkBand(WarnSeconds);
                FeedbackHub.Instance?.Cue(_forge.WorkPoint.position);
            }
            if (_warned && _t >= _nextShift)
            {
                _graceT = Mathf.Abs(_next - _center) * GracePerDegree;
                _center = _next;
                _next = PickNext();
                _nextShift = _t + Hold();
                _warned = false;
                _shifts++;
                _forge.SetBand(_center, HalfBand);
            }

            float temp = _forge.FireTemp;
            bool inBand = Mathf.Abs(temp - _center) <= HalfBand;
            if (_graceT > 0f) _graceT -= dt;
            if (_graceT <= 0f || inBand)
            {
                _counted += dt;
                if (inBand) _inBand += dt;
            }
            _sum += temp * dt;
            _min = Mathf.Min(_min, temp);
            _max = Mathf.Max(_max, temp);
            if (_forge.Pumping) _pumpT += dt;
            if (_t < Seconds) return;

            int score = Scoring.Round(Mathf.Clamp01(_inBand / Mathf.Max(1f, _counted)) * 100);
            Result = new MinigameResult { Score = score }
                .Add("inBandPct", "금색 칸에 머문 비율(%)", score)
                .Add("shifts", "금색 칸이 옮겨 간 횟수", _shifts)
                .Add("avg", "노 평균 온도(℃)", Scoring.Round(_sum / _t))
                .Add("max", "최고 온도(℃)", Scoring.Round(_max))
                .Add("min", "최저 온도(℃)", Scoring.Round(_min))
                .Add("pumpPct", "풀무질한 시간(%)", Scoring.Round(_pumpT / _t * 100));
            _forge.ShowOre(false);
            _forge.ResetBand();
            IsDone = true;
        }
    }
}
