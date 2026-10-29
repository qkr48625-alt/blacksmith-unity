using System;
using Blacksmith.Crafting;
using Blacksmith.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blacksmith.Minigames
{
    // 제련 ① 풀무질: 누르고 있으면 불이 세지고, 떼면 식는다. 1,100~1,200℃에 머문 시간 비율이 점수다.
    // 수치는 웹 버전 SmeltStage.jsx 와 같다(돌발 상황은 아직 옮기지 않았다).
    public sealed class BellowsMinigame : IMinigame
    {
        const float Seconds = 60f;
        const float TMin = 300f;
        const float TMax = 1400f;
        const float BandLo = 1100f;
        const float BandHi = 1200f;
        const float StartTemp = 600f;

        // 누르고 있으면 오르는 속도(℃/초). 뜨거울수록 더 올리기 힘들다. 금색 구간 근처에서 초당 약 60℃.
        static float RiseOf(float temp) => 140f * (1f - (temp - TMin) / 1500f);
        // 떼면 식는 속도(℃/초). 뜨거울수록 빨리 식는다. 금색 구간 근처에서 초당 약 75℃.
        static float CoolOf(float temp) => 25f + 0.06f * (temp - TMin);

        float _temp = StartTemp, _t, _inBand, _min = StartTemp, _max = StartTemp, _sum, _holdT;
        HoldInput _hold;
        Label _tempLabel, _status;
        Label _left;
        VisualElement _timeFill, _thermoFill, _thermoLine, _furnace;

        public bool IsDone { get; private set; }
        public MinigameResult Result { get; private set; }

        public void Build(VisualElement root, MinigameContext ctx)
        {
            var layout = Ui.Row("bellows");
            root.Add(layout);

            var left = Ui.Box("grow");
            layout.Add(left);
            _furnace = Ui.Box("panel", "furnace");
            _furnace.Add(Ui.Label("노 안 온도", "small", "muted"));
            _tempLabel = Ui.Label("", "big-number");
            _furnace.Add(_tempLabel);
            var band = Ui.Row();
            band.Add(Ui.Label($"금색 구간: {BandLo:N0} ~ {BandHi:N0} ℃ · ", "small", "muted"));
            _status = Ui.Label("", "small");
            band.Add(_status);
            _furnace.Add(band);
            left.Add(_furnace);
            left.Add(Ui.Bar(out _timeFill, "time-bar"));
            _left = Ui.Label("", "small", "muted", "align-right");
            left.Add(_left);

            // 온도계: 금색 구간을 막대 위에 겹쳐 그려서 차올라도 가려지지 않는다
            var right = Ui.Box("bellows__side");
            layout.Add(right);
            var thermoWrap = Ui.Box("thermo-wrap");
            var thermo = Ui.Box("thermo");
            _thermoFill = Ui.Box("thermo__fill");
            var bandBox = Ui.Box("thermo__band");
            bandBox.style.bottom = Length.Percent(Pct(BandLo));
            bandBox.style.height = Length.Percent(Pct(BandHi) - Pct(BandLo));
            _thermoLine = Ui.Box("thermo__line");
            thermo.Add(_thermoFill);
            thermo.Add(bandBox);
            thermo.Add(_thermoLine);
            thermoWrap.Add(thermo);
            foreach (var v in new[] { BandHi, BandLo })
            {
                var tick = Ui.Label($"{v:N0}", "thermo__tick");
                tick.style.bottom = Length.Percent(Pct(v));
                thermoWrap.Add(tick);
            }
            right.Add(thermoWrap);

            var button = Ui.HoldButton("풀무 ▲ 누르기", out _);
            right.Add(button);
            right.Add(Ui.Label("스페이스바도 됩니다", "tiny", "muted", "align-center"));
            _hold = new HoldInput(button);

            Render();
        }

        public void Tick(float dt)
        {
            if (IsDone) return;
            _hold.Poll();
            _t += dt;

            // 바람은 천천히 오르내리는 작은 흔들림(최대 초당 ±18℃)이다.
            float drift = 12f * Mathf.Sin(_t * 0.5f) + 6f * Mathf.Sin(_t * 1.3f + 1f);
            _temp += ((_hold.Held ? RiseOf(_temp) : -CoolOf(_temp)) + drift) * dt;
            _temp = Mathf.Clamp(_temp, TMin, TMax);
            if (_temp >= BandLo && _temp <= BandHi) _inBand += dt;
            _min = Math.Min(_min, _temp);
            _max = Math.Max(_max, _temp);
            _sum += _temp * dt;
            if (_hold.Held) _holdT += dt;

            if (_t >= Seconds)
            {
                int score = Scoring.Round(_inBand / Seconds * 100);
                Result = new MinigameResult { Score = score }
                    .Add("inBandPct", "금색 구간에 머문 비율(%)", score)
                    .Add("avg", "노 평균 온도(℃)", Scoring.Round(_sum / Seconds))
                    .Add("max", "최고 온도(℃)", Scoring.Round(_max))
                    .Add("min", "최저 온도(℃)", Scoring.Round(_min))
                    .Add("holdPct", "풀무를 누른 시간(%)", Scoring.Round(_holdT / Seconds * 100));
                _hold.Enabled = false;
                _hold.Reset();
                IsDone = true;
            }
            Render();
        }

        static float Pct(float v) => (v - TMin) / (TMax - TMin) * 100f;

        void Render()
        {
            bool inBand = _temp >= BandLo && _temp <= BandHi;
            _tempLabel.text = $"{Mathf.RoundToInt(_temp):N0} ℃";
            _tempLabel.EnableInClassList("amber", inBand);
            _status.text = inBand ? "좋습니다, 유지하세요" : _temp > BandHi ? "너무 뜨겁습니다" : "더 달궈야 합니다";
            _status.EnableInClassList("amber", inBand);
            _status.EnableInClassList("red", !inBand && _temp > BandHi);
            _status.EnableInClassList("sky", !inBand && _temp < BandLo);

            float left = Mathf.Max(0f, Seconds - _t);
            Ui.SetPercentWidth(_timeFill, left / Seconds);
            _left.text = $"남은 시간 {Mathf.CeilToInt(left)}초";

            _thermoFill.style.height = Length.Percent(Pct(_temp));
            _thermoLine.style.bottom = Length.Percent(Pct(_temp));

            // 노가 뜨거울수록 불빛이 밝아진다
            float heat = Mathf.Clamp01((_temp - 700f) / 600f);
            _thermoFill.style.backgroundColor = Color.Lerp(new Color32(124, 45, 18, 255), new Color32(253, 186, 116, 255), heat);
            _furnace.style.borderTopColor = _furnace.style.borderBottomColor =
                _furnace.style.borderLeftColor = _furnace.style.borderRightColor =
                    Color.Lerp(new Color32(41, 37, 36, 255), new Color32(251, 146, 60, 255), heat);
        }
    }
}
