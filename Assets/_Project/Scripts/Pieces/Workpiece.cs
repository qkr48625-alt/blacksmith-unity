using System;
using UnityEngine;

namespace Blacksmith.Pieces
{
    // 작업물이 공정을 지나며 바뀌는 모양. PartDef.producesState 에 이름으로 적는다.
    public enum WorkpieceState
    {
        None,
        Bloom,    // 블룸(철덩이)
        Bar,      // 철 바
        Blade,    // 칼날
        Hardened, // 담금질한 칼날
        Ground,   // 간 칼날
        Dagger,   // 손잡이를 단 단검
    }

    // 쇠 한 덩이. 온도를 지니고 다니며, 온도는 빛나는 색으로만 보인다.
    // 화덕 불 속에 있으면 불 온도로 다가가고, 밖에 있으면 공기 중에 식는다.
    public class Workpiece : MonoBehaviour
    {
        public const float RoomTemp = 20f;
        // 이 온도를 넘으면 쇠가 탄다(하얗게 빛나며 흰 불티). 철 바부터.
        public const float BurnTemp = 1300f;
        // 망치질이 제대로 먹는 가장 낮은 온도(체리빛). 아래에서 치면 점수가 깎인다.
        public const float WorkableTemp = 750f;

        // 공기 중 냉각(뉴턴 냉각). 1,150℃ → 750℃ 까지 약 36초(접기 한 판을 거의 한 번 달궈서 버틸 만큼).
        const float AirCooling = 0.012f;
        // 불 속에서 불 온도로 다가가는 빠르기. 불이 1,250℃면 실온에서 체리빛까지 약 2초.
        const float FireHeating = 0.45f;

        [SerializeField] Material metalMaterial;

        Material _mat;
        GameObject _visual;
        Func<float> _fireTemp;

        public WorkpieceState State { get; private set; }
        public float Temperature { get; set; } = RoomTemp;
        public bool CanBurn => State >= WorkpieceState.Bar;
        public bool IsBurning => CanBurn && Temperature > BurnTemp;
        public bool InFire => _fireTemp != null;
        // 이 작업물이 지금까지 탄 시간(초) — 저장된다
        public float BurnTotal { get; set; }
        // 마지막으로 점수에 반영한 뒤로 탄 시간(초)
        public float BurnSinceMark { get; private set; }

        public event Action<float> Burned;

        public void Init(Material metal) => metalMaterial = metal;

        public void SetState(WorkpieceState state)
        {
            State = state;
            if (_visual != null) Destroy(_visual);
            _visual = BuildVisual(state);
            ApplyGlow();
        }

        // 화덕 불 속에 넣는다. fireTemp 는 지금 불 온도를 돌려준다. null 이면 꺼낸다.
        public void SetFire(Func<float> fireTemp) => _fireTemp = fireTemp;

        // 겉 색을 바꾼다(뜨임 산화막 등). null 이면 원래 쇠 색으로.
        public void SetTint(Color? tint)
        {
            if (_mat == null) return;
            if (_baseColor == null) _baseColor = _mat.GetColor("_BaseColor");
            _mat.SetColor("_BaseColor", tint ?? _baseColor.Value);
        }

        Color? _baseColor;

        public float TakeBurnMark()
        {
            float v = BurnSinceMark;
            BurnSinceMark = 0f;
            return v;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_fireTemp != null) Temperature += (_fireTemp() - Temperature) * FireHeating * dt;
            else Temperature -= (Temperature - RoomTemp) * AirCooling * dt;

            if (IsBurning)
            {
                BurnTotal += dt;
                BurnSinceMark += dt;
                Burned?.Invoke(dt);
            }
            ApplyGlow();
        }

        // 온도 → 빛. 525℃(어둑한 붉은빛)에서 보이기 시작해 체리 → 주황 → 노랑 → 흰색으로 밝아진다.
        public static Color GlowColor(float temp)
        {
            if (temp < 500f) return Color.black;
            (float t, Color c)[] stops =
            {
                (500f, new Color(0f, 0f, 0f)),
                (650f, new Color(0.35f, 0.02f, 0f)),
                (800f, new Color(0.9f, 0.08f, 0.02f)),   // 체리빛
                (1000f, new Color(1f, 0.35f, 0.05f)),    // 주황
                (1150f, new Color(1f, 0.72f, 0.25f)),    // 노랑
                (1300f, new Color(1f, 0.95f, 0.75f)),    // 흰빛
            };
            for (int i = 1; i < stops.Length; i++)
            {
                if (temp > stops[i].t) continue;
                float k = Mathf.InverseLerp(stops[i - 1].t, stops[i].t, temp);
                return Color.Lerp(stops[i - 1].c, stops[i].c, k) * Mathf.Lerp(1f, 6f, Mathf.InverseLerp(650f, 1350f, temp));
            }
            return stops[^1].c * 7f;
        }

        void ApplyGlow()
        {
            if (_mat == null) return;
            _mat.SetColor("_EmissionColor", GlowColor(Temperature));
        }

        GameObject BuildVisual(WorkpieceState state)
        {
            var root = new GameObject(state.ToString());
            root.transform.SetParent(transform, false);
            if (_mat == null && metalMaterial != null)
            {
                _mat = new Material(metalMaterial);
                _mat.EnableKeyword("_EMISSION");
            }

            switch (state)
            {
                case WorkpieceState.Bloom:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.13f, 0.18f));
                    Part(root, PrimitiveType.Sphere, new Vector3(0.05f, 0.02f, 0.03f), new Vector3(0.12f, 0.1f, 0.12f));
                    break;
                case WorkpieceState.Bar:
                    Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.42f, 0.05f, 0.07f));
                    break;
                case WorkpieceState.Blade:
                case WorkpieceState.Hardened:
                case WorkpieceState.Ground:
                    Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.36f, 0.025f, 0.06f));
                    Part(root, PrimitiveType.Cube, new Vector3(-0.23f, 0f, 0f), new Vector3(0.1f, 0.02f, 0.02f)); // 슴베
                    break;
                case WorkpieceState.Dagger:
                    Part(root, PrimitiveType.Cube, new Vector3(0.08f, 0f, 0f), new Vector3(0.36f, 0.025f, 0.06f));
                    Part(root, PrimitiveType.Cylinder, new Vector3(-0.17f, 0f, 0f), new Vector3(0.04f, 0.06f, 0.04f), Quaternion.Euler(0, 0, 90), keepMetal: false);
                    break;
            }
            return root;
        }

        void Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion? rot = null, bool keepMetal = true)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            if (keepMetal && _mat != null) r.sharedMaterial = _mat;
            else r.material.color = new Color(0.35f, 0.22f, 0.12f);
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
