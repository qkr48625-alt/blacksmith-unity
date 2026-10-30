using System.Collections.Generic;
using UnityEngine;

namespace Blacksmith.World
{
    // 지금 가야 할 작업대를 알려 준다: 위에서 위아래로 떠다니는 금빛 화살표, 서는 자리의 빛나는 고리, 작업대를 비추는 불빛.
    // 대장장이가 작업대에 붙어 있거나 큰 창이 떠 있으면 숨긴다.
    public class QuestMarkers : MonoBehaviour
    {
        const float ArrowMaxHeight = 1.9f;   // 화덕 굴뚝처럼 높은 작업대도 화살표가 화면 밖으로 안 나가게
        static readonly Color Gold = new(1f, 0.72f, 0.25f);

        class Marker
        {
            public Station Station;
            public Transform Root, Arrow, Ring;
            public Light Light;
            public float ArrowY;
        }

        readonly List<Marker> _markers = new();
        Material _mat;
        Mesh _arrowMesh, _ringMesh;

        public bool Hidden { get; set; }

        public void Init(Material baseMaterial)
        {
            _mat = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _mat.SetColor("_BaseColor", Gold);
            _mat.EnableKeyword("_EMISSION");
            // 1 을 살짝 넘겨 Bloom 이 은은하게 번지게(너무 세면 하얗게 날아간다)
            _mat.SetColor("_EmissionColor", Gold * 1.15f);
            _arrowMesh = BuildArrowMesh();
            _ringMesh = BuildRingMesh(0.36f, 0.46f, 48);
        }

        // 표시할 작업대를 바꾼다. 같은 작업대는 그대로 둔다.
        public void Show(ICollection<Station> targets)
        {
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                if (targets.Contains(_markers[i].Station)) continue;
                Destroy(_markers[i].Root.gameObject);
                _markers.RemoveAt(i);
            }
            foreach (var s in targets)
                if (!_markers.Exists(m => m.Station == s)) _markers.Add(Create(s));
        }

        void Update()
        {
            float t = Time.time;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            foreach (var m in _markers)
            {
                m.Root.gameObject.SetActive(!Hidden);
                if (Hidden) continue;
                m.Arrow.localPosition = new Vector3(m.Arrow.localPosition.x, m.ArrowY + 0.12f * Mathf.Sin(t * 3f), m.Arrow.localPosition.z);
                m.Arrow.localRotation = Quaternion.Euler(0f, t * 90f, 0f);
                float ring = 0.9f + 0.12f * pulse;
                m.Ring.localScale = new Vector3(ring, 1f, ring);
                m.Light.intensity = 0.2f + 0.35f * pulse;
            }
        }

        Marker Create(Station s)
        {
            var root = new GameObject($"Quest Marker ({s.DisplayName})").transform;
            root.SetParent(transform, false);

            // 일하는 자리 바로 위, 서는 자리 쪽으로 조금 당겨서(벽에 붙은 작업대도 화면 위로 안 잘리게)
            var bounds = s.VisualBounds();
            var work = s.WorkPoint.position;
            var pull = s.Approach.position - work;
            pull.y = 0f;
            var top = work + pull * 0.6f;
            top.y = Mathf.Min(work.y + 0.45f, ArrowMaxHeight);

            var arrow = new GameObject("Arrow").transform;
            arrow.SetParent(root, false);
            arrow.localPosition = top;
            arrow.localScale = Vector3.one * 1.4f;
            arrow.gameObject.AddComponent<MeshFilter>().sharedMesh = _arrowMesh;
            var ar = arrow.gameObject.AddComponent<MeshRenderer>();
            ar.sharedMaterial = _mat;
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ring = new GameObject("Ring").transform;
            ring.SetParent(root, false);
            var at = s.Approach.position;
            ring.localPosition = new Vector3(at.x, 0.02f, at.z);
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
            var rr = ring.gameObject.AddComponent<MeshRenderer>();
            rr.sharedMaterial = _mat;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            var toFront = at - bounds.center;
            toFront.y = 0f;
            light.transform.localPosition = new Vector3(bounds.center.x, Mathf.Min(bounds.max.y, ArrowMaxHeight) + 0.2f, bounds.center.z) + toFront.normalized * 0.5f;
            light.type = LightType.Point;
            light.color = Gold;
            light.range = 2f;
            light.shadows = LightShadows.None;

            return new Marker { Station = s, Root = root, Arrow = arrow, Ring = ring, Light = light, ArrowY = top.y };
        }

        // 바닥에 눕힌 납작한 고리(위에서 보이는 면만)
        static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = d * outer;
                verts[i * 2 + 1] = d * inner;
                int o0 = i * 2, i0 = i * 2 + 1, o1 = (i + 1) % segments * 2, i1 = o1 + 1;
                int t = i * 6;
                tris[t] = o0; tris[t + 1] = i0; tris[t + 2] = o1;
                tris[t + 3] = i0; tris[t + 4] = i1; tris[t + 5] = o1;
            }
            var mesh = new Mesh { name = "Quest Ring", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 아래를 가리키는 화살표: 사각뿔 머리 + 네모 기둥. 면마다 꼭짓점을 따로 둬서 각진 빛이 나게 한다.
        static Mesh BuildArrowMesh()
        {
            const float h = 0.16f, head = 0.3f, w = 0.06f, shaft = 0.28f;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            // 머리: 끝이 원점, 밑면이 위
            var tip = Vector3.zero;
            var p0 = new Vector3(-h, head, -h);
            var p1 = new Vector3(h, head, -h);
            var p2 = new Vector3(h, head, h);
            var p3 = new Vector3(-h, head, h);
            Tri(tip, p0, p1);
            Tri(tip, p1, p2);
            Tri(tip, p2, p3);
            Tri(tip, p3, p0);
            Quad(p0, p3, p2, p1);

            // 기둥
            float y0 = head, y1 = head + shaft;
            var b0 = new Vector3(-w, y0, -w); var b1 = new Vector3(w, y0, -w); var b2 = new Vector3(w, y0, w); var b3 = new Vector3(-w, y0, w);
            var t0 = new Vector3(-w, y1, -w); var t1 = new Vector3(w, y1, -w); var t2 = new Vector3(w, y1, w); var t3 = new Vector3(-w, y1, w);
            Quad(b0, t0, t1, b1);
            Quad(b1, t1, t2, b2);
            Quad(b2, t2, t3, b3);
            Quad(b3, t3, t0, b0);
            Quad(t0, t3, t2, t1);

            var mesh = new Mesh { name = "Quest Arrow" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
