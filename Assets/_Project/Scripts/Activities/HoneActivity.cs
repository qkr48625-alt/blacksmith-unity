using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 연마 ② 날 세우기: 칼날을 눕혀 내려다본다. 뿌리 쪽 빛나는 원에서 손숫돌을 눌러 잡고, 날 선(금빛 선)을 따라 날끝까지 한 번에 민다.
    // 위아래 날을 번갈아 6번. 한 번의 점수 = 날 선에서 벗어난 평균 거리 × 끝까지 민 비율, 0.45초보다 빨리 밀면 ×0.7.
    // 수치는 웹 HoneStep.jsx 를 m 로 옮겼다(칼날 0.36m = 330칸).
    public sealed class HoneActivity : ActivityBase
    {
        const int Strokes = 6;
        const float Unit = 0.36f / 330f;
        const float Root = -0.18f, Tip = 0.18f, Start = -0.167f, End = 0.147f;
        const float StartR = 22f * Unit, FastSec = 0.45f;

        Transform _blade, _stone, _startMark;
        readonly List<Transform> _edgeLines = new();
        readonly List<int> _scores = new();
        Plane _plane;
        bool _stroking;
        float _strokeT, _devSum, _maxX;
        int _samples;

        public HoneActivity(PartRef part) : base(part) { }

        int Side => _scores.Count % 2 == 0 ? 1 : -1;   // 1 = 먼 쪽 날, -1 = 가까운 쪽 날

        public override string Hint => $"빛나는 원에서 눌러 잡고 금빛 날 선을 따라 날끝까지 한 번에 밀기 · {_scores.Count + 1}/{Strokes}번째 ({(Side > 0 ? "위" : "아래")} 날)";

        static float HalfW(float x) => 0.03f * Mathf.Clamp01((Tip - x) / (Tip - Root)) + 0.002f;

        protected override void Build()
        {
            // 숫돌 앞에 세운 손숫돌 받침대 위에 칼날을 눕힌다
            var w = Station.WorkPoint.position;
            var boardPos = new Vector3(w.x, 0.88f, w.z) + ToStand * 0.45f;
            var wood = ActivityKit.Mat(new Color(0.5f, 0.32f, 0.17f));
            var board = ActivityKit.Root("Hone Board", Props, boardPos, FacingStand);
            ActivityKit.Prim(PrimitiveType.Cube, board, Vector3.zero, new Vector3(0.5f, 0.03f, 0.18f), wood);
            ActivityKit.Prim(PrimitiveType.Cube, board, new Vector3(0f, -0.44f, 0f), new Vector3(0.06f, 0.86f, 0.06f), wood);
            ActivityKit.Prim(PrimitiveType.Cube, board, new Vector3(0f, -0.86f, 0f), new Vector3(0.3f, 0.03f, 0.2f), wood);
            _blade = ActivityKit.Root("Blade Frame", Props, boardPos + Vector3.up * 0.028f, FacingStand);
            _plane = new Plane(Vector3.up, _blade.position + Vector3.up * 0.015f);
            var gold = ActivityKit.Mat(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.7f, 0.2f) * 1.6f);
            foreach (int side in new[] { 1, -1 })
            {
                var line = new GameObject(side > 0 ? "Edge +" : "Edge -").transform;
                line.SetParent(_blade, false);
                for (int i = 0; i < 12; i++)
                {
                    float x0 = Mathf.Lerp(Root, Tip, i / 12f), x1 = Mathf.Lerp(Root, Tip, (i + 1) / 12f);
                    float z0 = -side * HalfW(x0), z1 = -side * HalfW(x1);
                    var mid = new Vector3((x0 + x1) / 2f, 0.015f, (z0 + z1) / 2f);
                    float len = Vector2.Distance(new Vector2(x0, z0), new Vector2(x1, z1));
                    float ang = -Mathf.Atan2(z1 - z0, x1 - x0) * Mathf.Rad2Deg;
                    ActivityKit.Prim(PrimitiveType.Cube, line, mid, new Vector3(len, 0.001f, 0.0015f), gold, Quaternion.Euler(0f, ang, 0f));
                }
                _edgeLines.Add(line);
            }
            _startMark = ActivityKit.Prim(PrimitiveType.Cylinder, _blade, Vector3.zero, new Vector3(StartR * 2f, 0.0008f, StartR * 2f),
                ActivityKit.Mat(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.8f, 0.3f) * 1.2f), name: "Start").transform;
            _stone = ActivityKit.Prim(PrimitiveType.Cube, Props, Vector3.zero, new Vector3(0.05f, 0.015f, 0.02f), ActivityKit.Mat(new Color(0.55f, 0.5f, 0.45f)), name: "Hand Stone").transform;
            Cam = ActivityKit.View(Station.transform, _blade.position + ToStand * 0.28f + Vector3.up * 0.45f, _blade.position, 38f);
            ShowSide();
        }

        protected override void OnEnter()
        {
            _stroking = false;
            PlacePiece(_blade.position, _blade.rotation);
        }

        void ShowSide()
        {
            _edgeLines[0].gameObject.SetActive(Side > 0);
            _edgeLines[1].gameObject.SetActive(Side < 0);
            _startMark.localPosition = new Vector3(Start, 0.016f, -Side * HalfW(Start));
        }

        protected override void Tick()
        {
            if (!ActivityKit.Aim(_plane, out var p)) return;
            var local = _blade.InverseTransformPoint(p);
            _stone.position = p + Vector3.up * 0.008f;
            _stone.rotation = _blade.rotation;

            if (!_stroking)
            {
                if (Click.Down && Vector2.Distance(new Vector2(local.x, local.z), new Vector2(Start, -Side * HalfW(Start))) <= StartR)
                {
                    _stroking = true;
                    _strokeT = 0f;
                    _devSum = 0f;
                    _samples = 0;
                    _maxX = local.x;
                }
                return;
            }
            _strokeT += Time.deltaTime;
            float x = Mathf.Clamp(local.x, Root, Tip);
            _devSum += Mathf.Abs(local.z - -Side * HalfW(x));
            _samples++;
            _maxX = Mathf.Max(_maxX, local.x);
            if (local.x >= End || !Click.Held) EndStroke();
        }

        void EndStroke()
        {
            _stroking = false;
            float dev = _samples > 0 ? _devSum / _samples : 16f * Unit;
            float reach = Mathf.Clamp01((_maxX - Start) / (End - Start));
            bool fast = _strokeT < FastSec && reach >= 1f;
            int sc = Scoring.Round(ActivityKit.Lin(dev, 3f * Unit, 16f * Unit) * reach * (fast ? 0.7f : 1f) * 100f);
            _scores.Add(sc);
            FeedbackHub.Instance?.Poke(_stone.position);
            Toast(reach < 1f ? $"끝까지 밀지 못했습니다 {sc}점" : fast ? $"너무 빨랐습니다 {sc}점" : sc >= 85 ? $"날이 섰습니다! {sc}점" : $"{sc}점");
            if (_scores.Count >= Strokes)
            {
                Finish(new MinigameResult { Score = Scoring.Round(_scores.Average()) }
                    .Add("strokes", "스트로크 점수", string.Join(" / ", _scores)));
                return;
            }
            ShowSide();
        }
    }
}
