using Blacksmith.Feedback;
using Blacksmith.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Stations
{
    // 화덕. 불 온도는 늘 살아 있고, 풀무로 바람을 넣어야 오른다. 쇠를 넣으면 불 온도로 달궈진다.
    // 풀무질은 마우스를 누른 채 아래로 끌기(손잡이가 내려감) 또는 스페이스를 누르고 있기.
    // 온도 규칙은 웹 버전 제련(SmeltStage.jsx)과 같다.
    public class ForgeStation : Station
    {
        public const float TMin = 300f;
        public const float TMax = 1400f;

        [SerializeField] Transform bellowsHandle;   // 손잡이 회전축
        [SerializeField] Transform bellowsBody;     // 풀무 주머니(눌리면 납작해짐)
        [SerializeField] Transform dialNeedle;      // 온도계 바늘 회전축
        [SerializeField] Light fireLight;
        [SerializeField] Renderer coals;
        [SerializeField] ParticleSystem embers;
        [SerializeField] GameObject oreVisual;      // 제련 중 숯 속 광석

        float _fire = 900f;
        float _handle;      // 0 = 올라감, 1 = 끝까지 내림
        float _air;         // 이번 프레임 바람 세기(0~1.3)
        float _whooshCooldown;
        Material _coalMat;
        float _t;

        public float FireTemp => _fire;
        public bool Pumping => _air > 0.05f;

        // 누르고 있으면 오르는 속도(℃/초). 뜨거울수록 더 올리기 힘들다.
        static float RiseOf(float temp) => 140f * (1f - (temp - TMin) / 1500f);
        // 바람이 없으면 식는 속도(℃/초). 뜨거울수록 빨리 식는다.
        static float CoolOf(float temp) => 25f + 0.06f * (temp - TMin);

        public void Bind(Transform handle, Transform body, Transform needle, Light light, Renderer coalRenderer, ParticleSystem emberSystem, GameObject ore)
        {
            bellowsHandle = handle;
            bellowsBody = body;
            dialNeedle = needle;
            fireLight = light;
            coals = coalRenderer;
            embers = emberSystem;
            oreVisual = ore;
        }

        public void ShowOre(bool on)
        {
            if (oreVisual != null) oreVisual.SetActive(on);
        }

        void Start()
        {
            if (coals != null)
            {
                _coalMat = coals.material;
                _coalMat.EnableKeyword("_EMISSION");
            }
            ShowOre(false);
        }

        public override void Exit()
        {
            base.Exit();
            _air = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            ReadBellows(dt);

            // 바람은 천천히 오르내리는 작은 흔들림(최대 초당 ±18℃)이다.
            float drift = 12f * Mathf.Sin(_t * 0.5f) + 6f * Mathf.Sin(_t * 1.3f + 1f);
            float rate = _air > 0.05f ? RiseOf(_fire) * Mathf.Min(_air, 1.3f) : -CoolOf(_fire);
            _fire = Mathf.Clamp(_fire + (rate + drift) * dt, TMin, TMax);

            if (_whooshCooldown > 0f) _whooshCooldown -= dt;
            Render();
        }

        void ReadBellows(float dt)
        {
            float prev = _handle;
            bool space = IsActive && Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            var mouse = Mouse.current;
            bool dragging = IsActive && mouse != null && mouse.leftButton.isPressed && !WorkshopGame.PointerOverUi;

            if (dragging)
            {
                // 화면 높이의 1/3 을 끌어내리면 손잡이가 끝까지 내려간다
                float dy = -mouse.delta.ReadValue().y / Mathf.Max(1, Screen.height) * 3f;
                _handle = Mathf.Clamp01(_handle + dy);
            }
            else if (space)
            {
                // 스페이스는 손잡이를 일정하게 오르내린다(웹의 "누르고 있기"와 같은 바람)
                _handle = 0.5f + 0.5f * Mathf.Sin(_t * 6f);
            }
            else
            {
                _handle = Mathf.MoveTowards(_handle, 0f, 2f * dt);
            }

            // 내려가는 빠르기만 바람이 된다. 0.6초에 끝까지 내리면 가득(1).
            float down = Mathf.Max(0f, _handle - prev) / Mathf.Max(dt, 1e-4f);
            _air = space ? 1f : down / 1.6f;

            if (_air > 0.4f && _whooshCooldown <= 0f)
            {
                FeedbackHub.Instance?.Whoosh(transform.position, _air);
                _whooshCooldown = 0.45f;
            }
        }

        void Render()
        {
            float heat = Mathf.InverseLerp(TMin, TMax, _fire);
            if (bellowsHandle != null) bellowsHandle.localRotation = Quaternion.Euler(0f, 0f, -35f * _handle);
            if (bellowsBody != null)
            {
                var s = bellowsBody.localScale;
                bellowsBody.localScale = new Vector3(s.x, Mathf.Lerp(0.35f, 0.18f, _handle), s.z);
            }
            // 바늘: 300℃ = -120°, 1,400℃ = +120°
            if (dialNeedle != null) dialNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(120f, -120f, heat));

            float flicker = 1f + 0.12f * Mathf.PerlinNoise(_t * 7f, 0.3f) - 0.06f + (Pumping ? 0.15f : 0f);
            if (fireLight != null)
            {
                fireLight.intensity = Mathf.Lerp(1.2f, 9f, heat * heat) * flicker;
                fireLight.color = Color.Lerp(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.75f, 0.4f), heat);
            }
            if (_coalMat != null) _coalMat.SetColor("_EmissionColor", Pieces.Workpiece.GlowColor(_fire) * 0.6f * flicker);
            if (embers != null)
            {
                var em = embers.emission;
                em.rateOverTime = Mathf.Lerp(1f, 30f, heat) + (Pumping ? 25f : 0f);
            }
        }
    }
}
