using Blacksmith.Crafting;
using Blacksmith.Minigames;
using Blacksmith.Stations;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 제련 ① 풀무질: 광석을 숯 속에 넣고 60초 동안 불을 1,100~1,200℃에 지킨다. 머문 시간 비율이 점수.
    // 불 온도 · 풀무 조작은 ForgeStation 이 맡고, 여기서는 시간과 측정값만 잰다(화덕을 떠나 있는 동안은 시간이 멈춘다).
    public sealed class BellowsActivity : IStationActivity
    {
        const float Seconds = 60f;
        const float BandLo = 1100f;
        const float BandHi = 1200f;

        ForgeStation _forge;
        float _t, _inBand, _sum, _pumpT, _min = float.MaxValue, _max;

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
                    _forge.FireTemp > BandHi ? "너무 뜨겁습니다" :
                    _forge.FireTemp >= BandLo ? "좋습니다, 유지하세요" : "더 달궈야 합니다";
                return $"마우스를 누른 채 아래로 끌어 풀무질 (스페이스도 됨) · 온도계 바늘을 금색 칸에 · {state} · 남은 {Mathf.CeilToInt(left)}초";
            }
        }

        public void Begin(ActivityContext ctx)
        {
            _forge = ctx.Station as ForgeStation;
            if (_forge == null) Debug.LogError("[Bellows] 화덕이 아닌 작업대에서 시작했습니다.");
        }

        public void Enter() => _forge?.ShowOre(true);

        public void Exit() { }

        public void Tick(float dt)
        {
            if (IsDone || _forge == null) return;
            _t += dt;
            float temp = _forge.FireTemp;
            if (temp >= BandLo && temp <= BandHi) _inBand += dt;
            _sum += temp * dt;
            _min = Mathf.Min(_min, temp);
            _max = Mathf.Max(_max, temp);
            if (_forge.Pumping) _pumpT += dt;
            if (_t < Seconds) return;

            int score = Scoring.Round(_inBand / Seconds * 100);
            Result = new MinigameResult { Score = score }
                .Add("inBandPct", "금색 구간에 머문 비율(%)", score)
                .Add("avg", "노 평균 온도(℃)", Scoring.Round(_sum / Seconds))
                .Add("max", "최고 온도(℃)", Scoring.Round(_max))
                .Add("min", "최저 온도(℃)", Scoring.Round(_min))
                .Add("pumpPct", "풀무질한 시간(%)", Scoring.Round(_pumpT / Seconds * 100));
            _forge.ShowOre(false);
            IsDone = true;
        }
    }
}
