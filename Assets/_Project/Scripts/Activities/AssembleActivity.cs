using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 손잡이 ② 조립: 작업대에 칼날이 누워 있고(슴베가 오른쪽), 앞 쟁반에 부품 다섯(아래 론델 · 그립 · 위 론델 · 폼멜 · 쐐기).
    // 부품을 클릭해 집고, R 또는 우클릭으로 뒤집고, 슴베 끝을 클릭해 끼운다. 순서: 아래 론델 → 그립 → 위 론델 → 폼멜.
    // 방향: 아래 론델은 금 테가 날 쪽(왼쪽), 위 론델은 금 테가 바깥(오른쪽), 그립은 굵은 쪽이 날 쪽. 쐐기는 필요 없다.
    // 틀린 부품 · 방향이면 실수(쟁반으로 돌아감). 점수 = 100 − 15 × 실수 − 8초 넘긴 시간 × 3, 20초 제한, 못 끼운 부품 하나당 −20.
    public sealed class AssembleActivity : ActivityBase
    {
        const float Limit = 20f;
        static readonly string[] Order = { "lower", "grip", "upper", "pommel" };
        static readonly Dictionary<string, string> Names = new()
            { ["lower"] = "아래 론델", ["grip"] = "그립", ["upper"] = "위 론델", ["pommel"] = "폼멜", ["drift"] = "쐐기" };

        class PartObj
        {
            public string Key;
            public Transform T;
            public Vector3 TrayPos;
            public bool Flipped;      // 지금 방향이 틀렸는가(방향 있는 부품만)
            public bool Directional;
            public bool Placed;
        }

        readonly List<PartObj> _parts = new();
        PartObj _held;
        Press _right;
        Plane _plane;
        Vector3 _tangEnd, _aim;
        int _placed, _mistakes;
        float _end;

        public AssembleActivity(PartRef part) : base(part) { }

        Vector3 Right => -Vector3.Cross(Vector3.up, ToStand);   // 대장장이가 보는 오른쪽

        public override string Hint => _held == null
            ? $"쟁반에서 다음 부품을 클릭해 집기 (순서: 아래 론델 → 그립 → 위 론델 → 폼멜) · {_placed}/4 · 남은 {Mathf.CeilToInt(Mathf.Max(0f, Limit - T))}초"
            : $"{Names[_held.Key]} 집음 · R/우클릭 = 뒤집기 · 슴베 끝(빛나는 곳)을 클릭해 끼우기 · 금 테/굵은 쪽: 아래 론델 · 그립은 날 쪽, 위 론델은 바깥쪽";

        protected override void Build()
        {
            var w = Station.WorkPoint.position;
            float top = 0.89f;
            _plane = new Plane(Vector3.up, new Vector3(0f, top + 0.02f, 0f));
            var rot = Quaternion.LookRotation(Right);   // +z = 오른쪽
            // 칼날(누워 있음): 날은 왼쪽, 슴베는 오른쪽 끝
            var blade = ActivityKit.Root("Blade", Props, new Vector3(w.x, top + 0.015f, w.z) + ToStand * 0.05f, rot);
            var steel = ActivityKit.Mat(new Color(0.75f, 0.76f, 0.78f));
            ActivityKit.Prim(PrimitiveType.Cube, blade, new Vector3(0f, 0f, -0.15f), new Vector3(0.06f, 0.01f, 0.3f), steel);
            ActivityKit.Prim(PrimitiveType.Cube, blade, new Vector3(0f, 0f, 0.09f), new Vector3(0.012f, 0.01f, 0.18f), steel);
            _tangEnd = blade.TransformPoint(new Vector3(0f, 0f, 0.17f));
            ActivityKit.Prim(PrimitiveType.Sphere, Props, Vector3.zero, Vector3.one * 0.02f, ActivityKit.Mat(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.7f, 0.2f) * 2f), name: "Tang Glow").transform.position = _tangEnd;
            _blade = blade;

            // 쟁반의 부품(순서를 섞고, 방향 있는 부품은 절반 확률로 뒤집혀 있다)
            var keys = new List<string> { "lower", "grip", "upper", "pommel", "drift" };
            for (int i = keys.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (keys[i], keys[j]) = (keys[j], keys[i]);
            }
            var trayCenter = new Vector3(w.x, top + 0.02f, w.z) + ToStand * 0.22f;
            ActivityKit.Prim(PrimitiveType.Cube, Props, Vector3.zero, new Vector3(0.36f, 0.01f, 0.1f), ActivityKit.Mat(new Color(0.3f, 0.2f, 0.12f)), Quaternion.LookRotation(ToStand) * Quaternion.Euler(0f, 90f, 0f), "Tray").transform.position = trayCenter + Vector3.down * 0.012f;
            for (int i = 0; i < keys.Count; i++)
            {
                var p = new PartObj { Key = keys[i], Directional = keys[i] is "lower" or "grip" or "upper" };
                p.TrayPos = trayCenter + Right * (-0.14f + i * 0.07f);
                p.Flipped = p.Directional && Rng.NextDouble() < 0.5;
                p.T = MakePart(p.Key, rot);
                _parts.Add(p);
                ResetPart(p);
            }
            Cam = ActivityKit.View(Station.transform, w + ToStand * 0.55f + Vector3.up * 0.5f, w + ToStand * 0.1f, 40f);
        }

        Transform _blade;

        Transform MakePart(string key, Quaternion rot)
        {
            var t = ActivityKit.Root(key, Props, Vector3.zero, rot);
            var brass = ActivityKit.Mat(new Color(0.75f, 0.58f, 0.25f));
            var gold = ActivityKit.Mat(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.7f, 0.2f) * 1.2f);
            var wood = ActivityKit.Mat(new Color(0.5f, 0.32f, 0.17f));
            var axis = Quaternion.Euler(90f, 0f, 0f);   // 원기둥 축을 +z(슴베 방향)로
            switch (key)
            {
                case "lower":
                case "upper":
                    ActivityKit.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.05f, 0.005f, 0.05f), brass, axis);
                    // 금 테: 기본 방향에서 아래 론델은 왼쪽(-z), 위 론델은 오른쪽(+z)
                    ActivityKit.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, key == "lower" ? -0.006f : 0.006f), new Vector3(0.054f, 0.0015f, 0.054f), gold, axis);
                    break;
                case "grip":
                    ActivityKit.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, -0.02f), new Vector3(0.034f, 0.02f, 0.034f), wood, axis);   // 굵은 쪽 = 왼쪽
                    ActivityKit.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, 0.02f), new Vector3(0.026f, 0.02f, 0.026f), wood, axis);
                    break;
                case "pommel":
                    ActivityKit.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.04f, 0.04f, 0.03f), brass);
                    break;
                default:
                    ActivityKit.Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.012f, 0.012f, 0.04f), ActivityKit.Mat(new Color(0.45f, 0.45f, 0.48f)));
                    break;
            }
            return t;
        }

        void ResetPart(PartObj p)
        {
            p.T.position = p.TrayPos;
            p.T.rotation = Quaternion.LookRotation(Right) * (p.Flipped ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
        }

        protected override void OnEnter()
        {
            _right.Reset();
            // 진짜 작업물은 작업대 밑에 두고 조립용 칼날 소품을 쓴다
            PlacePiece(Station.WorkPoint.position + Vector3.down * 0.45f, Quaternion.identity);
        }

        protected override void Tick()
        {
            _right.Update(ActivityKit.RightHeld);
            if (_end > 0f)
            {
                _end -= Time.deltaTime;
                if (_end <= 0f) Done();
                return;
            }
            if (T >= Limit)
            {
                Toast("시간이 다 됐습니다");
                _end = 0.8f;
                return;
            }
            if (ActivityKit.Aim(_plane, out var a)) _aim = a;

            if (_held != null)
            {
                _held.T.position = _aim + Vector3.up * 0.03f;
                if (_right.Down || ActivityKit.KeyDown(Key.R))
                {
                    _held.Flipped = !_held.Flipped;
                    _held.T.rotation = Quaternion.LookRotation(Right) * (_held.Flipped ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
                }
                if (!Click.Down) return;
                if (Vector3.Distance(Flat(_aim), Flat(_tangEnd)) <= 0.07f) TryPlace(_held);
                else ResetPart(_held);
                _held = null;
                return;
            }

            if (!Click.Down) return;
            var pick = _parts.Where(p => !p.Placed).OrderBy(p => Vector3.Distance(Flat(_aim), Flat(p.TrayPos))).FirstOrDefault();
            if (pick != null && Vector3.Distance(Flat(_aim), Flat(pick.TrayPos)) <= 0.04f) _held = pick;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        void TryPlace(PartObj p)
        {
            bool right = _placed < Order.Length && p.Key == Order[_placed] && !p.Flipped;
            if (!right)
            {
                _mistakes++;
                FeedbackHub.Instance?.Hit(_tangEnd, 0);
                Toast(p.Key == "drift" ? "쐐기는 필요 없습니다" : p.Flipped && _placed < Order.Length && p.Key == Order[_placed] ? $"{Names[p.Key]} 방향이 거꾸로입니다" : $"지금은 {Names[Order[_placed]]} 차례입니다");
                ResetPart(p);
                return;
            }
            p.Placed = true;
            // 슴베를 따라 날 쪽부터 쌓인다
            float[] at = { 0.01f, 0.045f, 0.08f, 0.1f };
            p.T.position = _blade.TransformPoint(new Vector3(0f, 0f, at[_placed]));
            p.T.rotation = Quaternion.LookRotation(Right);
            _placed++;
            FeedbackHub.Instance?.Done(_tangEnd);
            if (_placed >= Order.Length) _end = 0.8f;
        }

        void Done()
        {
            int missing = Order.Length - _placed;
            int sc = Mathf.Clamp(Scoring.Round(100 - 15 * _mistakes - Mathf.Max(0f, Mathf.Min(T, Limit) - 8f) * 3f - 20 * missing), 0, 100);
            Finish(new MinigameResult { Score = sc }
                .Add("mistakes", "실수", _mistakes)
                .Add("seconds", "걸린 시간(초)", Mathf.Round(Mathf.Min(T, Limit) * 10f) / 10f)
                .Add("missing", "못 끼운 부품", missing));
        }
    }
}
