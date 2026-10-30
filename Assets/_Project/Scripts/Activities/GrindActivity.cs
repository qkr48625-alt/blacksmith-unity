using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 연마 ① 숫돌 갈기: 숫돌을 옆에서 본다. 마우스를 누른 채 숫돌 둘레로 원을 그리면 크랭크가 돌아 숫돌이 돈다.
    // 알맞은 속도(초당 0.8~1.3바퀴)에서 잘 갈리고, 빠를수록 칼날이 달아오른다(끝이 푸르게 변함). 스페이스/우클릭 = 물 찍기.
    // 칼날 끝까지 다 갈리거나 45초가 지나면 끝. 점수 = 50% 갈린 정도 + 30% 알맞은 속도 비율 + 20% (1 − 과열 비율). 수치는 웹 GrindStage.jsx 와 같다.
    public sealed class GrindActivity : ActivityBase
    {
        const float Limit = 45f, Slow = 0.5f, IdealLo = 0.8f, IdealHi = 1.3f, Fast = 1.8f, Water = 0.35f, WaterCooldown = 1.2f;

        Transform _wheel, _ground;
        Press _right;
        float _progress, _heat, _grindT, _idealT, _overT, _waterCd, _omega, _lastAngle, _sparkT;
        bool _hasAngle;
        Vector3 _center;

        public GrindActivity(PartRef part) : base(part) { }

        public override string Hint
        {
            get
            {
                string speed = _omega < Slow ? "더 빨리 돌리세요" : _omega < IdealLo ? "조금 더 빨리" : _omega <= IdealHi ? "알맞은 속도!" : _omega <= Fast ? "조금 빠릅니다" : "너무 빠릅니다";
                string hot = _heat >= 1f ? " · 칼날이 달아올랐습니다! 물(스페이스/우클릭)" : _heat > 0.7f ? " · 칼날이 뜨거워집니다" : "";
                return $"누른 채 숫돌 둘레로 원 그리기 · {speed} · 갈린 날 {Scoring.Round(_progress * 100)}%{hot} · 남은 {Mathf.CeilToInt(Mathf.Max(0f, Limit - T))}초";
            }
        }

        protected override void Build()
        {
            _wheel = Station.transform.Find("Wheel");
            _center = _wheel != null ? _wheel.position : Station.WorkPoint.position + Vector3.down * 0.25f;
            // 갈린 자리: 칼날 위를 따라 은빛 띠가 늘어난다
            _ground = ActivityKit.Prim(PrimitiveType.Cube, Props, Vector3.zero, new Vector3(0.001f, 0.004f, 0.012f),
                ActivityKit.Mat(new Color(0.9f, 0.92f, 0.95f), new Color(0.6f, 0.65f, 0.7f) * 0.6f), name: "Ground Edge").transform;
            // 숫돌 면을 왼쪽 앞 위에서 비스듬히 본다(바로 옆은 나무 틀이 가린다)
            Cam = ActivityKit.View(Station.transform, _center + new Vector3(-0.75f, 0.6f, -0.75f), _center + Vector3.up * 0.15f, 42f);
        }

        protected override void OnEnter()
        {
            _right.Reset();
            _hasAngle = false;
            if (Piece != null) PlacePiece(Station.WorkPoint.position, Quaternion.Euler(0f, 90f, 0f));
        }

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            _right.Update(ActivityKit.RightHeld);

            // 크랭크: 화면에 비친 숫돌 가운데를 중심으로 마우스 각도가 도는 빠르기
            float omega = 0f;
            var cam = Camera.main;
            if (Click.Held && cam != null)
            {
                Vector2 c = cam.WorldToScreenPoint(_center);
                var d = ActivityKit.MouseScreen - c;
                if (d.magnitude > 20f)
                {
                    float a = Mathf.Atan2(d.y, d.x);
                    if (_hasAngle) omega = Mathf.Abs(Mathf.DeltaAngle(_lastAngle * Mathf.Rad2Deg, a * Mathf.Rad2Deg)) / 360f / Mathf.Max(dt, 1e-4f);
                    _lastAngle = a;
                    _hasAngle = true;
                }
            }
            else _hasAngle = false;
            _omega = Mathf.Lerp(_omega, Mathf.Min(omega, 3f), 1f - Mathf.Exp(-dt * 6f));
            if (!Click.Held) _omega = Mathf.MoveTowards(_omega, 0f, dt * 1.5f);
            if (_wheel != null) _wheel.Rotate(Vector3.up, _omega * 360f * dt, UnityEngine.Space.Self);

            if (_omega >= Slow)
            {
                bool ideal = _omega >= IdealLo && _omega <= IdealHi;
                _grindT += dt;
                if (ideal) _idealT += dt;
                _progress = Mathf.Min(1f, _progress + 0.045f * Mathf.Min(_omega, Fast) * (ideal ? 1f : 0.6f) * dt);
                _heat += (0.025f + 0.06f * _omega * _omega) * Mod("grindHeat", 1f) * dt;
                if (_heat >= 1f) _overT += dt;
                _sparkT -= dt;
                if (_sparkT <= 0f)
                {
                    _sparkT = 0.06f;
                    FeedbackHub.Instance?.Grind(Station.WorkPoint.position, Mathf.Clamp01(_omega / Fast));
                }
            }
            _heat = Mathf.Clamp(_heat - 0.03f * dt, 0f, 1.2f);

            if (_waterCd > 0f) _waterCd -= dt;
            if ((Space.Down || _right.Down) && _waterCd <= 0f)
            {
                _heat = Mathf.Max(0f, _heat - Water);
                _waterCd = WaterCooldown;
                FeedbackHub.Instance?.Steam(Station.WorkPoint.position);
            }

            // 보이기: 갈린 띠 길이, 달아오르면 칼날이 푸르게
            if (Piece != null)
            {
                Piece.SetTint(Color.Lerp(new Color(0.75f, 0.76f, 0.78f), new Color(0.2f, 0.35f, 0.85f), Mathf.InverseLerp(0.6f, 1.1f, _heat)));
                _ground.position = Piece.transform.position + Vector3.up * 0.014f + Piece.transform.right * (0.18f - 0.18f * _progress);
                _ground.rotation = Piece.transform.rotation;
                _ground.localScale = new Vector3(Mathf.Max(0.001f, 0.36f * _progress), 0.004f, 0.012f);
            }

            if (_progress >= 1f || T >= Limit) Done();
        }

        void Done()
        {
            float ideal = _grindT > 0f ? _idealT / _grindT : 0f;
            float over = _grindT > 0f ? Mathf.Min(1f, _overT / _grindT) : 0f;
            Piece?.SetTint(null);
            Finish(new MinigameResult { Score = Scoring.Round(100f * (0.5f * _progress + 0.3f * ideal + 0.2f * (1f - over))) }
                .Add("coveragePct", "갈린 정도(%)", Scoring.Round(_progress * 100f))
                .Add("idealPct", "알맞은 속도 비율(%)", Scoring.Round(ideal * 100f))
                .Add("overheatPct", "과열 비율(%)", Scoring.Round(over * 100f))
                .Add("seconds", "걸린 시간(초)", Scoring.Round(T)));
        }
    }
}
