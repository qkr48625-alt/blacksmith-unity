using Unity.Cinemachine;
using UnityEngine;

namespace Blacksmith.Stations
{
    // 화덕 앞면의 노 단면 창. 녹은 슬래그 기둥 · 표면 선 · 쇠꼬챙이 · 배출구에서 쏟아지는 줄기 · 바닥 홈의 슬래그를 그린다.
    // 판정은 SlagActivity 가 하고, 여기서는 보여 주기만 한다. 슬래그를 빼는 동안만 가까운 카메라로 넘어간다.
    public class SlagPit : MonoBehaviour
    {
        const float PokeSeconds = 0.35f;

        [SerializeField] Transform column;      // 슬래그 기둥 축(바닥 기준, y 크기 = 높이 0~1)
        [SerializeField] Transform surface;     // 슬래그 표면의 밝은 선
        [SerializeField] Transform poker;       // 쇠꼬챙이 축(+z 가 배출구 쪽)
        [SerializeField] Transform stream;      // 배출구에서 바닥 홈으로 떨어지는 줄기
        [SerializeField] Renderer puddle;       // 바닥 홈에 고인 슬래그
        [SerializeField] Transform spoutTip;    // 배출구 끝(불티 · 소리 자리)
        [SerializeField] CinemachineCamera view;
        [SerializeField] float height = 0.72f;  // 높이 1 이 몇 m 인가

        Vector3 _pokerRest, _surfaceRest;
        (Transform t, Vector3 rest)[] _marks;   // 판정 눈금(금색 선 · 은 · 동) — 목표 높이에 따라 위아래로 옮긴다
        const float MarksCenter = 0.68f;        // 씬에서 눈금이 놓인 높이(SlagActivity.Center)
        float _pokeT, _puddleHeat, _t;
        bool _pouring;
        Material _puddleMat;

        public Vector3 SpoutTip => spoutTip != null ? spoutTip.position : transform.position;

        public void Bind(Transform col, Transform surf, Transform pokerRoot, Transform pour, Renderer pool, Transform tip, CinemachineCamera cam, float h)
        {
            column = col;
            surface = surf;
            poker = pokerRoot;
            stream = pour;
            puddle = pool;
            spoutTip = tip;
            view = cam;
            height = h;
        }

        void Awake()
        {
            if (poker != null) _pokerRest = poker.localPosition;
            if (surface != null) _surfaceRest = surface.localPosition;
            var marks = new System.Collections.Generic.List<(Transform, Vector3)>();
            foreach (Transform c in transform)
                if (c.name is "Gold Line" or "Gold Mark" or "Silver Mark" or "Bronze Mark") marks.Add((c, c.localPosition));
            _marks = marks.ToArray();
            if (puddle != null)
            {
                _puddleMat = puddle.material;
                _puddleMat.EnableKeyword("_EMISSION");
            }
            SetPouring(false);
            SetLevel(0.12f);
            Show(false);
        }

        // 슬래그를 빼는 동안 단면 창을 가까이 보는 카메라(작업대 시점보다 위)
        public void Show(bool on)
        {
            if (view != null) view.Priority = on ? 30 : 0;
        }

        public void SetLevel(float level)
        {
            level = Mathf.Clamp(level, 0.001f, 1f);
            if (column != null) column.localScale = new Vector3(1f, level, 1f);
            if (surface != null) surface.localPosition = _surfaceRest + Vector3.up * (level * height);
        }

        // 판정 눈금을 목표 높이(0~1)로 옮긴다
        public void SetTarget(float level)
        {
            if (_marks == null) return;
            foreach (var (t, rest) in _marks) t.localPosition = rest + Vector3.up * ((level - MarksCenter) * height);
        }

        public void Poke() => _pokeT = PokeSeconds;

        public void SetPouring(bool on)
        {
            _pouring = on;
            if (stream != null) stream.gameObject.SetActive(on);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;

            // 쇠꼬챙이: 빠르게 찔렀다가 천천히 빠진다
            if (poker != null)
            {
                float k = 0f;
                if (_pokeT > 0f)
                {
                    _pokeT -= dt;
                    float u = 1f - _pokeT / PokeSeconds;
                    k = u < 0.3f ? u / 0.3f : 1f - (u - 0.3f) / 0.7f;
                }
                poker.localPosition = _pokerRest + poker.localRotation * Vector3.forward * (0.12f * k);
            }

            // 줄기는 굵기가 출렁이고, 홈에 고인 슬래그는 붓는 동안 달아올랐다가 천천히 식는다
            if (stream != null && _pouring)
            {
                var s = stream.localScale;
                stream.localScale = new Vector3(0.035f + 0.012f * Mathf.Sin(_t * 23f), s.y, 0.035f + 0.01f * Mathf.Sin(_t * 17f + 1f));
            }
            _puddleHeat = _pouring ? Mathf.MoveTowards(_puddleHeat, 1f, dt * 1.5f) : Mathf.MoveTowards(_puddleHeat, 0.15f, dt * 0.08f);
            if (_puddleMat != null) _puddleMat.SetColor("_EmissionColor", new Color(1f, 0.28f, 0.04f) * (1.4f * _puddleHeat));
        }
    }
}
