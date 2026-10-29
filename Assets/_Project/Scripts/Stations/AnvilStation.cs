using Blacksmith.World;
using UnityEngine;

namespace Blacksmith.Stations
{
    // 모루. 망치를 들어 올린 높이가 곧 힘이고, 옆 눈금 기둥의 금색 띠가 이번에 필요한 힘이다.
    // 망치를 내리치는 움직임과 작업물 놓는 자리만 맡고, 판정은 활동(FoldActivity 등)이 한다.
    public class AnvilStation : Station
    {
        const float PostHeight = 0.8f;

        [SerializeField] Transform hammerPivot;   // 망치 회전축(손잡이 끝)
        [SerializeField] Transform targetBand;    // 눈금 기둥의 금색 띠
        [SerializeField] Transform powerMarker;   // 눈금 기둥의 흰 표시(지금 힘)

        float _power;
        float _strikeT;   // 내리치는 중이면 남은 시간
        float _strikeFrom;

        public void Bind(Transform hammer, Transform band, Transform marker)
        {
            hammerPivot = hammer;
            targetBand = band;
            powerMarker = marker;
        }

        public override void Enter()
        {
            base.Enter();
            SetGaugeVisible(false);
        }

        public override void Exit()
        {
            base.Exit();
            _power = 0f;
            SetGaugeVisible(false);
        }

        public void SetGaugeVisible(bool on)
        {
            if (targetBand != null) targetBand.gameObject.SetActive(on);
            if (powerMarker != null) powerMarker.gameObject.SetActive(on);
        }

        // 금색 띠: center ± half (0~1 힘)
        public void ShowTarget(float center, float half)
        {
            SetGaugeVisible(true);
            if (targetBand == null) return;
            targetBand.localPosition = new Vector3(targetBand.localPosition.x, center * PostHeight, targetBand.localPosition.z);
            var s = targetBand.localScale;
            targetBand.localScale = new Vector3(s.x, Mathf.Max(0.01f, half * 2f * PostHeight), s.z);
        }

        public void SetPower(float power) => _power = Mathf.Clamp01(power);

        public void Strike()
        {
            _strikeFrom = _power;
            _strikeT = 0.09f;
            _power = 0f;
        }

        void Update()
        {
            float angle;
            if (_strikeT > 0f)
            {
                _strikeT -= Time.deltaTime;
                angle = Mathf.Lerp(0f, _strikeFrom * 80f, Mathf.Clamp01(_strikeT / 0.09f));
            }
            else angle = _power * 80f;

            if (hammerPivot != null) hammerPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (powerMarker != null)
                powerMarker.localPosition = new Vector3(powerMarker.localPosition.x, _power * PostHeight, powerMarker.localPosition.z);
        }
    }
}
