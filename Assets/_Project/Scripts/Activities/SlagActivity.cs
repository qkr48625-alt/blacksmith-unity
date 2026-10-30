using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Pieces;
using Blacksmith.Stations;
using Blacksmith.UI;
using Blacksmith.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 제련 ② 슬래그 배출: 화덕 앞면 단면 창에 녹은 슬래그가 차오른다. 배출구보다 높고 송풍관보다 낮은 때
    // 클릭(또는 스페이스)으로 쇠꼬챙이를 찔러 배출구를 뚫으면 흘러나온다. 4번.
    // 판정 구간은 창 가운데 금색 선(금) · 옆 틀의 은색 · 구리색 눈금(은 · 동). 수치는 웹 버전 SlagTapStep.jsx 와 같다.
    // 갓 나온 블룸은 그동안 숯 속에 넣어 두어 식지 않게 한다.
    public sealed class SlagActivity : IStationActivity
    {
        public const int Taps = 4;
        public const float Hole = 0.5f;     // 배출구 높이. 이보다 낮으면 뚫어도 흘러나오지 않는다
        public const float Tuyere = 0.86f;  // 송풍관 높이. 여기까지 차오르면 바람이 막힌다
        public const float Center = 0.68f;  // 가장 좋은 때
        public static readonly (string key, string name, float half, int score)[] Zones =
        {
            ("gold", "금", 0.015f, 100),
            ("silver", "은", 0.04f, 80),
            ("bronze", "동", 0.08f, 55),
        };
        const float Drain = 1.3f;

        struct TapRec
        {
            public int Score;
            public string Zone;
            public float Level;
            public bool Choked;
        }

        ForgeStation _forge;
        SlagPit _pit;
        Workpiece _piece;
        Hud _hud;
        System.Random _rng;
        float _t, _level = 0.12f, _shown = 0.12f, _rate, _phase, _drain, _drainFrom;
        bool _active, _wasHeld = true;
        readonly List<TapRec> _taps = new();

        public SlagActivity(PartRef part) => Part = part;

        public PartRef Part { get; }
        public bool IsDone { get; private set; }
        public MinigameResult Result { get; private set; }

        public string Hint
        {
            get
            {
                string last = _taps.Count == 0 ? "" : " · " + string.Join(" ", _taps.Select(x => x.Zone == null ? "×" : ZoneName(x.Zone)));
                return $"클릭(또는 스페이스)으로 배출구 뚫기 · 슬래그 표면이 가운데 금색 선에 올 때 · 옆 눈금 은 · 동까지 합격 · {_taps.Count}/{Taps}{last}";
            }
        }

        static string ZoneName(string key) => Zones.First(z => z.key == key).name;

        void NewRise()
        {
            _rate = 0.12f + (float)_rng.NextDouble() * 0.08f;
            _phase = (float)_rng.NextDouble() * 6f;
        }

        public void Begin(ActivityContext ctx)
        {
            _forge = ctx.Station as ForgeStation;
            _pit = _forge != null ? _forge.GetComponentInChildren<SlagPit>(true) : null;
            _piece = ctx.Piece;
            _hud = ctx.Hud;
            _rng = ctx.Game.Rng;
            NewRise();
            if (_pit == null) Debug.LogError("[Slag] 화덕에 슬래그 단면 창(SlagPit)이 없습니다. 씬을 다시 지어 주세요.");
        }

        public void Enter()
        {
            _active = true;
            _wasHeld = true;
            _pit?.Show(true);
            _pit?.SetLevel(_shown);
            KeepBloomHot(true);
        }

        public void Exit()
        {
            _active = false;
            _pit?.Show(false);
            _pit?.SetPouring(false);
            KeepBloomHot(false);
        }

        // 블룸은 숯 속에 묻어 둔다(작업대를 떠나면 WorkshopGame 이 다시 집게로 든다)
        void KeepBloomHot(bool on)
        {
            if (_piece == null || _forge == null) return;
            if (on)
            {
                _piece.transform.SetParent(_forge.WorkPoint, false);
                _piece.transform.localPosition = Vector3.zero;
                _piece.SetFire(() => _forge.FireTemp);
            }
            else _piece.SetFire(null);
        }

        public void Tick(float dt)
        {
            if (IsDone || !_active || _pit == null) return;
            _t += dt;

            bool held = (Mouse.current != null && Mouse.current.leftButton.isPressed && !WorkshopGame.PointerOverUi)
                        || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            bool pressed = held && !_wasHeld;
            _wasHeld = held;

            if (_drain > 0f)
            {
                // 뚫린 구멍으로 빠진다. 구멍 높이 아래로는 빠지지 않는다
                _drain -= dt;
                float floor = _drainFrom >= Hole ? 0.1f : _drainFrom;
                _level = floor + (_drainFrom - floor) * Mathf.Max(0f, _drain / Drain);
                _shown = _level;
                if (_drain <= 0f)
                {
                    _pit.SetPouring(false);
                    _level = Mathf.Min(_level, 0.2f);
                    _shown = _level;
                    NewRise();
                    if (_taps.Count >= Taps)
                    {
                        Finish();
                        return;
                    }
                }
            }
            else
            {
                float surge = 1f + 0.45f * Mathf.Sin(_t * 1.9f + _phase);
                _level += _rate * surge * dt;
                // 녹은 슬래그 표면이 보글보글 출렁인다(판정은 보이는 높이로)
                _shown = _level + 0.01f * Mathf.Sin(_t * 9f) + 0.006f * Mathf.Sin(_t * 23f);
                if (_level >= Tuyere) Choke();
                else if (pressed) Tap();
            }
            _pit.SetLevel(_shown);
        }

        void Tap()
        {
            var (sc, zone, word) = Judge(_shown);
            _taps.Add(new TapRec { Score = sc, Zone = zone, Level = Mathf.Round(_shown * 1000f) / 10f });
            _pit.Poke();
            FeedbackHub.Instance?.Poke(_pit.SpoutTip);
            if (_shown >= Hole)
            {
                _pit.SetPouring(true);
                FeedbackHub.Instance?.Pour(_pit.SpoutTip);
            }
            if (zone == "gold") FeedbackHub.Instance?.Done(_pit.SpoutTip);
            _hud?.Toast(sc > 0 ? $"{word} {sc}점" : word);
            StartDrain();
        }

        void Choke()
        {
            _taps.Add(new TapRec { Score = 0, Zone = null, Level = Tuyere * 100f, Choked = true });
            _hud?.Toast("송풍관이 막혔습니다! 0점");
            FeedbackHub.Instance?.Hit(_pit.SpoutTip, 0);
            _pit.SetPouring(true);
            FeedbackHub.Instance?.Pour(_pit.SpoutTip);
            StartDrain();
        }

        void StartDrain()
        {
            _drainFrom = _shown;
            _drain = Drain;
        }

        static (int score, string zone, string word) Judge(float level)
        {
            if (level < Hole) return (0, null, "구멍보다 낮아 거의 안 나옵니다");
            float d = Mathf.Abs(level - Center);
            foreach (var z in Zones)
                if (d <= z.half)
                    return (z.score, z.key, z.key switch { "gold" => "정확히 맞췄습니다! 금", "silver" => "좋습니다! 은", _ => "합격입니다. 동" });
            return (0, null, level < Center ? "너무 일렀습니다" : "너무 늦었습니다");
        }

        void Finish()
        {
            int avg = Scoring.Round(_taps.Average(x => x.Score));
            int Count(string k) => _taps.Count(x => x.Zone == k);
            Result = new MinigameResult { Score = avg }
                .Add("taps", "배출 점수", string.Join(" / ", _taps.Select(x => x.Score)))
                .Add("levels", "뚫은 높이(%)", string.Join(" / ", _taps.Select(x => x.Level.ToString("0.#"))))
                .Add("zones", "금 · 은 · 동", $"{Count("gold")} · {Count("silver")} · {Count("bronze")}")
                .Add("choked", "송풍관 막힘", _taps.Count(x => x.Choked));
            _pit.Show(false);
            KeepBloomHot(false);
            IsDone = true;
        }
    }
}
