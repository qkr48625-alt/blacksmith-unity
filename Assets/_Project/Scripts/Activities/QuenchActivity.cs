using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Pieces;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 열처리 ① 담금질: 달군 칼날을 물통 위에 들고 있으면 김을 맞으며 식어 간다. 옆 견본처럼 체리빛(800℃)일 때
    // 클릭(또는 스페이스)으로 물에 담근다. 점수는 담근 온도(800℃ ± 120℃ 안이면 60점 이상).
    // 너무 식었으면(600℃ 아래) 화덕에서 다시 달궈 오는 게 낫다. 웹 TemperStage.jsx 의 담금질을 실제 온도로 옮겼다.
    public sealed class QuenchActivity : ActivityBase
    {
        const float Cherry = 800f, Half = 120f;
        const float ExtraCooling = 0.05f;   // 물통 위 김을 맞아 공기보다 조금 빨리 식는다

        Vector3 _above, _under;
        float _dunk, _end, _temp;
        bool _done;

        public QuenchActivity(PartRef part) : base(part) { }

        public override string Hint
        {
            get
            {
                if (_done) return "담금질 끝";
                float t = Piece != null ? Piece.Temperature : 0f;
                string state = t > Cherry + Half ? "아직 너무 뜨겁습니다 — 기다리세요" : t >= Cherry - Half ? "체리빛! 지금 담그세요" : t >= 600f ? "식어 갑니다!" : "너무 식었습니다 — 화덕에서 달궈 오세요";
                return $"칼날이 옆 견본(체리빛)과 같아지면 클릭(또는 스페이스)으로 담그기 · {state}";
            }
        }

        protected override void Build()
        {
            var w = Station.WorkPoint.position;
            _above = w + Vector3.up * 0.1f;
            _under = new Vector3(w.x, 0.6f, w.z);
            // 견본은 물통 테두리에 올려 둔다
            var right = Vector3.Cross(Vector3.up, ToStand);
            var rim = w + ToStand * 0.3f + right * 0.25f;
            HeatSamples.Glow(Props, new Vector3(rim.x, 0.72f, rim.z), FacingStand, Cherry, true);
            Cam = ActivityKit.View(Station.transform, w + ToStand * 0.75f + Vector3.up * 0.4f, w + Vector3.down * 0.05f, 40f);
        }

        protected override void OnEnter()
        {
            Piece?.SetFire(null);
            PlacePiece(_dunk > 0f || _done ? _under : _above, FacingStand);
        }

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            if (_done)
            {
                if (_dunk > 0f)
                {
                    _dunk -= dt;
                    if (Piece != null) Piece.transform.position = Vector3.Lerp(_under, _above, Mathf.Clamp01(_dunk / 0.25f));
                }
                _end -= dt;
                if (Piece != null) Piece.Temperature = Mathf.MoveTowards(Piece.Temperature, Workpiece.RoomTemp, 900f * dt);
                if (_end <= 0f) Done();
                return;
            }
            if (Piece != null)
            {
                Piece.Temperature -= (Piece.Temperature - Workpiece.RoomTemp) * ExtraCooling * dt;
                if (Random.value < dt * 2f) FeedbackHub.Instance?.Steam(_under + Vector3.up * 0.12f);
            }
            if (Click.Down || Space.Down) Dunk();
        }

        void Dunk()
        {
            _temp = Piece != null ? Piece.Temperature : 0f;
            _done = true;
            _dunk = 0.25f;
            _end = 1.6f;
            FeedbackHub.Instance?.Steam(_under + Vector3.up * 0.1f);
            FeedbackHub.Instance?.Pour(_under + Vector3.up * 0.1f);
            int sc = Gauge.Score(_temp, Cherry, Half);
            Toast(sc >= 85 ? $"체리빛 딱 그 자리! {sc}점" : _temp > Cherry ? $"너무 뜨거울 때 담갔습니다 {sc}점" : $"식은 뒤에 담갔습니다 {sc}점");
        }

        void Done()
        {
            Finish(new MinigameResult { Score = Gauge.Score(_temp, Cherry, Half) }
                .Add("temp", "담근 온도(℃)", Scoring.Round(_temp))
                .Add("off", "체리빛과 차이(℃)", Scoring.Round(Mathf.Abs(_temp - Cherry))));
        }
    }
}
