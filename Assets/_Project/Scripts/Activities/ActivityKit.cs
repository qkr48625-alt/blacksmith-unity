using System.Collections.Generic;
using Blacksmith.World;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blacksmith.Activities
{
    // 활동들이 함께 쓰는 것: 실행 중에 만드는 소품(기본 도형 + 색 재질), 가까운 카메라, 누름 판정, 마우스 조준.
    // 소품은 활동마다 하나의 뿌리 아래에 만들고, 작업대를 떠나면 숨기고 끝나면 지운다.
    public static class ActivityKit
    {
        static readonly Dictionary<(Color, Color), Material> Mats = new();

        public static Material Mat(Color color, Color? emission = null)
        {
            var key = (color, emission ?? Color.black);
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.35f);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            Mats[key] = m;
            return m;
        }

        // 개별 색을 바꿀 소품용(공유하지 않는 재질)
        public static Material OwnMat(Color color, Color? emission = null)
        {
            var m = new Material(Mat(color, emission));
            if (emission.HasValue) m.EnableKeyword("_EMISSION");
            return m;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null, string name = null)
        {
            var g = GameObject.CreatePrimitive(type);
            if (name != null) g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static Transform Root(string name, Transform parent, Vector3 worldPos, Quaternion? worldRot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, true);
            t.SetPositionAndRotation(worldPos, worldRot ?? Quaternion.identity);
            return t;
        }

        // 활동 동안만 쓰는 가까운 카메라(작업대 시점보다 위)
        public static CinemachineCamera View(Transform parent, Vector3 pos, Vector3 look, float fov = 40f)
        {
            var c = new GameObject("CM Activity").AddComponent<CinemachineCamera>();
            c.transform.SetParent(parent, true);
            c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            c.Lens.FieldOfView = fov;
            c.Lens.NearClipPlane = 0.03f;
            c.Priority = 0;
            c.gameObject.AddComponent<CinemachineImpulseListener>();
            return c;
        }

        public static void SetView(CinemachineCamera c, bool on)
        {
            if (c != null) c.Priority = on ? 30 : 0;
        }

        public static bool MouseHeld => Mouse.current != null && Mouse.current.leftButton.isPressed && !WorkshopGame.PointerOverUi;
        public static bool RightHeld => Mouse.current != null && Mouse.current.rightButton.isPressed && !WorkshopGame.PointerOverUi;
        public static bool SpaceHeld => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        public static bool KeyDown(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;

        // 마우스 광선이 평면과 만나는 점
        public static bool Aim(Plane plane, out Vector3 point)
        {
            point = default;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null) return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!plane.Raycast(ray, out float d)) return false;
            point = ray.GetPoint(d);
            return true;
        }

        public static Vector2 MouseScreen => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        // 손 떨림: 느린 물결 두 개(웹 engine/tremor.js 와 같은 모양). amp 는 m 단위.
        public static Vector2 Tremor(float t, float amp) =>
            new(amp * (0.6f * Mathf.Sin(t * 2.3f) + 0.4f * Mathf.Sin(t * 5.1f + 1.3f)),
                amp * (0.6f * Mathf.Sin(t * 1.7f + 0.7f) + 0.4f * Mathf.Sin(t * 4.3f + 2.1f)));

        // 거리 d 가 full 이하면 1, zero 이상이면 0, 사이는 직선
        public static float Lin(float d, float full, float zero) => d <= full ? 1f : d >= zero ? 0f : (zero - d) / (zero - full);
    }

    // 새로 누른 순간만 잡는다(작업대를 고른 클릭이 그대로 조작이 되지 않게 처음엔 "눌려 있었음"으로 둔다)
    public struct Press
    {
        bool _was;
        public bool Held { get; private set; }
        public bool Down { get; private set; }
        public bool Up { get; private set; }

        public void Reset() => _was = true;

        public void Update(bool held)
        {
            Held = held;
            Down = held && !_was;
            Up = !held && _was;
            _was = held;
        }
    }
}
