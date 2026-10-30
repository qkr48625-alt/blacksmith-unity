using System.Collections.Generic;
using System.IO;
using Blacksmith.Activities;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Stations;
using Blacksmith.World;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Blacksmith.EditorTools
{
    // 3D 미니어처 대장간 그레이박스 씬을 기본 도형으로 짓는다(재설계 §3 배치).
    // 방은 x -6~6, z -4.5~4.5(m). 카메라는 앞(-z)에서 비스듬히 내려다본다.
    // 이미 씬이 있으면 건드리지 않는다. 다시 짓고 싶으면 메뉴 Blacksmith/Rebuild Workshop Scene.
    public static class WorkshopSceneBuilder
    {
        public const string ScenePath = ProjectSetup.Root + "/Scenes/Workshop.unity";
        const string MatDir = ProjectSetup.Root + "/Art/Materials";

        static readonly Dictionary<string, Material> Mats = new();

        [MenuItem("Blacksmith/Rebuild Workshop Scene")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("대장간 씬 다시 짓기", "Workshop.unity 를 지우고 다시 짓습니다. 씬에서 손으로 고친 것은 사라집니다.", "다시 짓기", "취소")) return;
            RenderSetup.Ensure();
            Ensure(rebuild: true);
        }

        public static void RebuildBatch()
        {
            try
            {
                RenderSetup.Ensure();
                Ensure(rebuild: true);
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        // 다시 지을 때는 재질 색 · 후처리 값도 이 파일의 값으로 되돌린다
        static bool _refresh;

        public static void Ensure(bool rebuild)
        {
            if (File.Exists(ScenePath) && !rebuild) return;
            _refresh = rebuild;
            if (rebuild) RenderSetup.TuneVolume();
            EnsureMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 씬을 연 뒤에 에셋을 읽는다(씬을 열면 안 쓰는 에셋이 내려가 먼저 읽은 참조가 빈 참조로 저장된다)
            Mats.Clear();
            EnsureMaterials();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(ProjectSetup.PanelPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProjectSetup.StylePath);
            var dagger = AssetDatabase.LoadAssetAtPath<RecipeDef>(ProjectSetup.DaggerPath);
            var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderSetup.VolumePath);

            BuildLighting(volumeProfile);
            var room = BuildRoom();
            var player = BuildPlayer();
            var overview = BuildCameras(player.transform);

            var forge = BuildForge(new Vector3(-4f, 0f, 3f), room);
            var anvil = BuildAnvil(new Vector3(-0.9f, 0f, 2.5f), room);
            BuildQuench(new Vector3(2.4f, 0f, 3.1f), room);
            BuildGrind(new Vector3(4.5f, 0f, 2.4f), room);
            BuildBench(new Vector3(4.2f, 0f, -1.8f), room);
            BuildBoard(new Vector3(0.7f, 0f, 4.4f), room);
            var rack = BuildRack(new Vector3(5.92f, 0f, 0.6f), room);

            var fx = new GameObject("Feedback");
            var hub = fx.AddComponent<FeedbackHub>();
            SetField(hub, "sparkMaterial", Mats["Spark"]);
            var impulse = fx.GetComponent<CinemachineImpulseSource>();
            impulse.DefaultVelocity = Vector3.down * 0.25f;

            var game = new GameObject("WorkshopGame");
            game.AddComponent<UIDocument>().panelSettings = panel;
            var wg = game.AddComponent<WorkshopGame>();
            wg.Bind(new List<RecipeDef> { dagger }, style, player, room.GetComponent<NavMeshSurface>(), Mats["HotMetal"], rack);
            EditorUtility.SetDirty(wg);

            EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Setup] {ScenePath} 를 지었습니다 (overview={overview.name}, forge={forge.name}, anvil={anvil.name})");
        }

        // ── 재질 ────────────────────────────────────────

        static void EnsureMaterials()
        {
            EnsureFolder(MatDir);
            // 바닥 · 벽은 차가운 회색으로 눌러 두고, 작업대(나무 · 쇠)와 대장장이가 그 위에서 도드라지게 한다
            Lit("Floor", new Color(0.2f, 0.2f, 0.21f), 0f, 0.15f);
            Lit("Wall", new Color(0.33f, 0.32f, 0.31f), 0f, 0.05f);
            Lit("Lantern", new Color(1f, 0.85f, 0.6f), 0f, 0.3f, new Color(1f, 0.7f, 0.4f) * 3f);
            Lit("Stone", new Color(0.5f, 0.47f, 0.44f), 0f, 0.1f);
            Lit("Wood", new Color(0.55f, 0.34f, 0.17f), 0f, 0.25f);
            // 반사할 하늘이 없어서 금속성을 높이면 새까맣게 보인다. 밝은 회청색 + 중간 금속성.
            Lit("Iron", new Color(0.46f, 0.48f, 0.52f), 0.45f, 0.55f);
            Lit("Leather", new Color(0.36f, 0.2f, 0.12f), 0f, 0.35f);
            Lit("Water", new Color(0.05f, 0.1f, 0.14f), 0f, 0.95f);
            Lit("Brass", new Color(0.75f, 0.58f, 0.25f), 0.9f, 0.6f);
            Lit("Paper", new Color(0.86f, 0.8f, 0.66f), 0f, 0.05f);
            // 대장장이는 갈색 대장간에서 한눈에 보이게 푸른 옷
            Lit("Player", new Color(0.24f, 0.42f, 0.66f), 0f, 0.3f);
            Lit("Apron", new Color(0.3f, 0.19f, 0.1f), 0f, 0.3f);
            Lit("Coal", new Color(0.05f, 0.04f, 0.04f), 0f, 0.1f, new Color(1f, 0.3f, 0.05f) * 1.5f);
            Lit("HotMetal", new Color(0.28f, 0.27f, 0.27f), 0.8f, 0.45f, Color.black);
            Lit("Gold", new Color(1f, 0.75f, 0.2f), 0f, 0.5f, new Color(1f, 0.7f, 0.15f) * 1.6f);
            Lit("Marker", Color.white, 0f, 0.5f, Color.white * 1.8f);
            Lit("Soot", new Color(0.06f, 0.05f, 0.05f), 0f, 0.1f);
            Lit("Copper", new Color(0.76f, 0.47f, 0.24f), 0.3f, 0.5f);
            Lit("Silver", new Color(0.84f, 0.83f, 0.82f), 0.3f, 0.6f);
            // 진한 주황(너무 밝으면 Bloom 에 노랗게 날아가 금색 눈금이 묻힌다)
            Lit("Slag", new Color(0.35f, 0.08f, 0.02f), 0f, 0.7f, new Color(1f, 0.28f, 0.04f) * 1.1f);
            Spark();
        }

        static Material Lit(string name, Color color, float metallic, float smooth, Color? emission = null)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && _refresh)
            {
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smooth);
                if (emission.HasValue) m.SetColor("_EmissionColor", emission.Value);
                EditorUtility.SetDirty(m);
            }
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smooth);
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                AssetDatabase.CreateAsset(m, path);
            }
            Mats[name] = m;
            return m;
        }

        // 불티 · 김: 더하기 섞기 파티클(빛나는 점)
        static void Spark()
        {
            var path = $"{MatDir}/Spark.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f); // Additive
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(m, path);
            }
            Mats["Spark"] = m;
        }

        // ── 조명 · 카메라 ───────────────────────────────

        static void BuildLighting(VolumeProfile profile)
        {
            // 위는 밝고 바닥 쪽은 어두운 세 갈래 주변광(검게 뭉개지지 않게)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // 주황빛을 덜어 낸 중립 톤(모든 게 갈색으로 뭉개지지 않게)
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.57f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.39f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.19f, 0.18f);
            RenderSettings.skybox = null;

            // 창으로 드는 낮빛(주 조명). 그림자는 옅게 해서 작업대 아래가 새까맣지 않게.
            var sun = new GameObject("Window Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(52f, -30f, 0f);

            // 천장 등불: 뒤쪽 둘은 매단 등이 보이고, 앞쪽은 빛만 둔다(앞에 매달면 카메라와 대장장이 사이를 가린다)
            var lanterns = new GameObject("Lanterns").transform;
            foreach (var (p, visible) in new[] { (new Vector3(-2.2f, 2.8f, 1.4f), true), (new Vector3(2.2f, 2.8f, 1.4f), true), (new Vector3(0f, 2.8f, -2.4f), false) })
            {
                var l = new GameObject("Lantern Light").AddComponent<Light>();
                l.transform.SetParent(lanterns, false);
                l.transform.position = p;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.85f, 0.65f);
                l.intensity = 2.2f;
                l.range = 7f;
                if (!visible) continue;
                Prim("Lantern", PrimitiveType.Cube, lanterns, p + new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.2f, 0.14f), "Lantern", collider: false);
                Prim("Cord", PrimitiveType.Cube, lanterns, p + new Vector3(0f, 0.45f, 0f), new Vector3(0.015f, 0.5f, 0.015f), "Iron", collider: false);
            }

            // 작업대 위 등잔
            var lamp = new GameObject("Bench Lamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.75f, 0.45f);
            lamp.intensity = 0.9f;
            lamp.range = 5f;
            lamp.transform.position = new Vector3(3.8f, 2.4f, -1.2f);

            var vol = new GameObject("Post Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        static CinemachineCamera BuildCameras(Transform player)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.035f, 0.03f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.7f);
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 8f, -7.5f), Quaternion.Euler(50f, 0f, 0f));

            // 전체 시점: 대장장이를 부드럽게 따라가되 방향은 고정
            var overview = new GameObject("CM Overview").AddComponent<CinemachineCamera>();
            overview.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            overview.Follow = player;
            overview.Priority = 10;
            overview.Lens.FieldOfView = 40f;
            var follow = overview.gameObject.AddComponent<CinemachineFollow>();
            // 조금 더 가까이(작업대가 작게 보이지 않게). 뒤로 덜 물러나서 화면 가운데가 대장장이보다 1.8m 앞을 본다
            // — 작업대가 뒤쪽 벽을 따라 놓여 있어서, 대장장이는 화면 아래쪽에 두고 앞을 넓게 보여 준다.
            follow.FollowOffset = new Vector3(0f, 6.4f, -3.6f);
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(0.6f, 0.6f, 0.6f);
            follow.TrackerSettings = tracker;
            overview.gameObject.AddComponent<CinemachineImpulseListener>();
            return overview;
        }

        // 작업대 시점 카메라: pos 에서 look 을 바라본다. 평소엔 우선순위 0, 붙으면 20.
        static CinemachineCamera StationCam(string name, Vector3 pos, Vector3 look)
        {
            var c = new GameObject($"CM {name}").AddComponent<CinemachineCamera>();
            c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            c.Priority = 0;
            c.Lens.FieldOfView = 42f;
            c.gameObject.AddComponent<CinemachineImpulseListener>();
            return c;
        }

        // ── 방 · 대장장이 ───────────────────────────────

        static GameObject BuildRoom()
        {
            var room = new GameObject("Room");
            Box("Floor", room.transform, new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 9f), "Floor");
            Box("Wall Back", room.transform, new Vector3(0f, 1.5f, 4.6f), new Vector3(12.4f, 3f, 0.2f), "Wall");
            Box("Wall Left", room.transform, new Vector3(-6.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 9.4f), "Wall");
            Box("Wall Right", room.transform, new Vector3(6.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 9.4f), "Wall");
            // 앞벽은 카메라를 가리지 않게 낮게, 가운데에 문 틈
            Box("Wall Front L", room.transform, new Vector3(-3.6f, 0.3f, -4.6f), new Vector3(5f, 0.6f, 0.2f), "Wall");
            Box("Wall Front R", room.transform, new Vector3(3.6f, 0.3f, -4.6f), new Vector3(5f, 0.6f, 0.2f), "Wall");
            Box("Ore Crate", room.transform, new Vector3(-5.2f, 0.3f, -0.5f), new Vector3(0.8f, 0.6f, 0.8f), "Wood");
            Box("Coal Sack", room.transform, new Vector3(-5.2f, 0.25f, 0.6f), new Vector3(0.7f, 0.5f, 0.6f), "Leather");

            var surface = room.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return room;
        }

        static PlayerController BuildPlayer()
        {
            // 몸 조각은 Prim 이 월드 좌표로 놓으므로, 원점에서 조립한 뒤에 자리로 옮긴다.
            // (먼저 옮기면 몸이 중심축에서 1.2m 떨어져 붙어, 돌 때 큰 원을 그리며 움직인다)
            var p = new GameObject("Blacksmith");
            Prim("Body", PrimitiveType.Capsule, p.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.55f, 0.8f, 0.45f), "Player", collider: false);
            Prim("Head", PrimitiveType.Sphere, p.transform, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.32f, "Player", collider: false);
            Prim("Apron", PrimitiveType.Cube, p.transform, new Vector3(0f, 0.75f, 0.21f), new Vector3(0.42f, 0.7f, 0.05f), "Apron", collider: false);
            // 집게: 손에서 앞으로 뻗고 끝에 작업물이 물린다
            Prim("Tongs", PrimitiveType.Cube, p.transform, new Vector3(0.22f, 1f, 0.42f), new Vector3(0.03f, 0.03f, 0.5f), "Iron", collider: false);
            var hold = new GameObject("Hold Point").transform;
            hold.SetParent(p.transform, false);
            hold.localPosition = new Vector3(0.22f, 1f, 0.72f);
            hold.localRotation = Quaternion.Euler(0f, 90f, 0f);
            p.transform.position = new Vector3(0f, 0f, -1.2f);

            var agent = p.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.enabled = false; // 길을 구운 뒤 WorkshopGame 이 켠다
            var pc = p.AddComponent<PlayerController>();
            pc.Bind(hold);
            return pc;
        }

        // ── 작업대 ─────────────────────────────────────

        static Station Make<T>(string name, Vector3 at, GameObject room, StationRole role, StationKind kind,
            Vector3 approach, Vector3 work, CinemachineCamera cam) where T : Station
        {
            var go = new GameObject(name);
            go.transform.SetParent(room.transform, false);
            go.transform.position = at;
            var a = new GameObject("Approach").transform;
            a.SetParent(go.transform, false);
            a.position = approach;
            var w = new GameObject("Work Point").transform;
            w.SetParent(go.transform, false);
            w.position = work;
            var s = go.AddComponent<T>();
            s.Setup(name, role, kind, a, w, cam);
            return s;
        }

        static ForgeStation BuildForge(Vector3 at, GameObject room)
        {
            var cam = StationCam("Forge", at + new Vector3(0.9f, 2.1f, -2.4f), at + new Vector3(0.5f, 0.8f, 0f));
            var s = (ForgeStation)Make<ForgeStation>("화덕", at, room, StationRole.Work, StationKind.Forge,
                at + new Vector3(0.6f, 0f, -1.4f), at + new Vector3(0f, 1.02f, -0.1f), cam);
            var t = s.transform;
            Box("Base", t, at + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.9f, 1.4f), "Stone");
            var coals = Box("Coals", t, at + new Vector3(0f, 0.96f, 0f), new Vector3(1.2f, 0.12f, 0.9f), "Coal");
            Box("Hood", t, at + new Vector3(0f, 2.2f, 0.15f), new Vector3(2f, 0.15f, 1.5f), "Stone");
            Box("Chimney", t, at + new Vector3(0f, 2.9f, 0.35f), new Vector3(0.6f, 1.3f, 0.6f), "Stone");

            var ore = new GameObject("Ore").transform;
            ore.SetParent(t, false);
            for (int i = 0; i < 4; i++)
                Prim($"Ore {i}", PrimitiveType.Sphere, ore, at + new Vector3(-0.3f + i * 0.2f, 1.05f, 0.1f - (i % 2) * 0.15f), Vector3.one * 0.14f, "Iron", collider: false);

            var light = new GameObject("Fire Light").AddComponent<Light>();
            light.transform.SetParent(t, false);
            light.transform.position = at + new Vector3(0f, 1.5f, -0.3f);
            light.type = LightType.Point;
            light.range = 7f;
            light.shadows = LightShadows.Soft;

            var embers = MakeEmbers(t, at + new Vector3(0f, 1.05f, 0f));

            // 풀무: 화덕 오른쪽. 손잡이 축은 주머니 오른쪽 위, 손잡이는 +x 로 뻗는다(내리면 아래로 기운다)
            var bellowsAt = at + new Vector3(1.55f, 0f, 0.1f);
            Box("Bellows Stand", t, bellowsAt + new Vector3(0f, 0.22f, 0f), new Vector3(0.7f, 0.44f, 0.55f), "Wood");
            var body = Box("Bellows Bag", t, bellowsAt + new Vector3(0f, 0.6f, 0f), new Vector3(0.75f, 0.35f, 0.5f), "Leather").transform;
            var pivot = new GameObject("Bellows Handle Pivot").transform;
            pivot.SetParent(t, false);
            pivot.position = bellowsAt + new Vector3(0.38f, 0.85f, 0f);
            var handle = Box("Handle", pivot, pivot.position + new Vector3(0.35f, 0f, 0f), new Vector3(0.7f, 0.05f, 0.06f), "Wood", collider: false);
            Box("Grip", handle.transform.parent, pivot.position + new Vector3(0.7f, 0f, 0f), new Vector3(0.06f, 0.06f, 0.25f), "Wood", collider: false);

            // 놋쇠 다이얼 온도계: 화덕 앞면. 금색 칸(폭 100℃)은 회전축에 달려 있어 풀무질 중에 옮겨 간다.
            var dial = new GameObject("Dial").transform;
            dial.SetParent(t, false);
            dial.position = at + new Vector3(0.55f, 0.55f, -0.72f);
            Prim("Face", PrimitiveType.Cylinder, dial, dial.position, new Vector3(0.36f, 0.01f, 0.36f), "Paper", Quaternion.Euler(90f, 0f, 0f), collider: false);
            Prim("Rim", PrimitiveType.Cylinder, dial, dial.position + new Vector3(0f, 0f, 0.01f), new Vector3(0.4f, 0.01f, 0.4f), "Brass", Quaternion.Euler(90f, 0f, 0f), collider: false);
            var band = new GameObject("Band Pivot").transform;
            band.SetParent(dial, false);
            band.position = dial.position + new Vector3(0f, 0f, -0.012f);
            const float degPerC = 240f / (ForgeStation.TMax - ForgeStation.TMin);
            for (float off = -50f; off <= 50f; off += 20f)
            {
                float a = -off * degPerC * Mathf.Deg2Rad;   // 온도가 높을수록 시계 방향
                var p = band.position + new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * 0.14f;
                Prim("Band", PrimitiveType.Cube, band, p, new Vector3(0.035f, 0.035f, 0.01f), "Gold", Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), collider: false);
            }
            var needle = new GameObject("Needle Pivot").transform;
            needle.SetParent(dial, false);
            needle.position = dial.position + new Vector3(0f, 0f, -0.02f);
            Box("Needle", needle, needle.position + new Vector3(0f, 0.07f, 0f), new Vector3(0.012f, 0.14f, 0.008f), "Iron", collider: false);

            s.Bind(pivot, body, needle, light, coals.GetComponent<Renderer>(), embers, ore.gameObject, band);
            BuildSlagPit(at, t);
            return s;
        }

        // 슬래그 배출: 화덕 앞면 왼쪽의 노 단면 창. 왼쪽에 배출구(아래로 바닥 홈), 오른쪽에 송풍관, 앞에 쇠꼬챙이.
        // 판정 눈금 높이는 SlagActivity 의 수치(배출구 · 송풍관 · 금 · 은 · 동)를 그대로 쓴다.
        static void BuildSlagPit(Vector3 at, Transform forge)
        {
            const float H = 0.72f, Y0 = 0.09f, W = 0.44f;
            float face = at.z - 0.7f;
            float cx = at.x - 0.35f;
            float Y(float level) => Y0 + level * H;

            var root = new GameObject("Slag Pit").transform;
            root.SetParent(forge, false);
            root.position = new Vector3(cx, 0f, face);

            // 창 안쪽(그을음) · 쇠 테두리
            Box("Window Back", root, new Vector3(cx, Y0 + H / 2f, face - 0.005f), new Vector3(W, H, 0.01f), "Soot", collider: false);
            Box("Frame Top", root, new Vector3(cx, Y0 + H + 0.02f, face - 0.02f), new Vector3(W + 0.08f, 0.04f, 0.03f), "Iron", collider: false);
            Box("Frame Bottom", root, new Vector3(cx, Y0 - 0.02f, face - 0.02f), new Vector3(W + 0.08f, 0.04f, 0.03f), "Iron", collider: false);
            foreach (float side in new[] { -1f, 1f })
                Box(side < 0 ? "Frame L" : "Frame R", root, new Vector3(cx + side * (W / 2f + 0.02f), Y0 + H / 2f, face - 0.02f), new Vector3(0.04f, H, 0.03f), "Iron", collider: false);

            // 녹은 슬래그 기둥: 축이 바닥에 있어 y 크기 = 높이
            var column = new GameObject("Slag Column").transform;
            column.SetParent(root, false);
            column.position = new Vector3(cx, Y0, face - 0.012f);
            Box("Slag", column, new Vector3(cx, Y0 + H / 2f, face - 0.012f), new Vector3(W, H, 0.01f), "Slag", collider: false);
            var surface = Box("Slag Surface", root, new Vector3(cx, Y0, face - 0.03f), new Vector3(W, 0.008f, 0.004f), "Marker", collider: false).transform;

            // 판정 눈금: 가운데 금색 선은 창을 가로지르고, 은 · 동은 양옆 틀에 튀어나온 눈금
            float c = Y(SlagActivity.Center);
            Box("Gold Line", root, new Vector3(cx, c, face - 0.024f), new Vector3(W, 2f * SlagActivity.Zones[0].half * H, 0.004f), "Gold", collider: false);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = cx + side * (W / 2f + 0.02f);
                Box("Bronze Mark", root, new Vector3(x, c, face - 0.035f), new Vector3(0.05f, 2f * SlagActivity.Zones[2].half * H, 0.03f), "Copper", collider: false);
                Box("Silver Mark", root, new Vector3(x, c, face - 0.045f), new Vector3(0.06f, 2f * SlagActivity.Zones[1].half * H, 0.04f), "Silver", collider: false);
                Box("Gold Mark", root, new Vector3(x, c, face - 0.055f), new Vector3(0.07f, 2f * SlagActivity.Zones[0].half * H, 0.05f), "Gold", collider: false);
            }

            // 배출구: 창 왼쪽, 앞으로 튀어나온 쇠 관
            float sx = cx - W / 2f - 0.08f, sy = Y(SlagActivity.Hole);
            Prim("Spout", PrimitiveType.Cylinder, root, new Vector3(sx, sy, face - 0.12f), new Vector3(0.09f, 0.12f, 0.09f), "Iron", Quaternion.Euler(90f, 0f, 0f), collider: false);
            var tip = new GameObject("Spout Tip").transform;
            tip.SetParent(root, false);
            tip.position = new Vector3(sx, sy, face - 0.24f);

            // 송풍관: 창 오른쪽, 풀무 쪽으로 뻗는다
            Prim("Tuyere", PrimitiveType.Cylinder, root, new Vector3(cx + W / 2f + 0.19f, Y(SlagActivity.Tuyere), face - 0.05f), new Vector3(0.08f, 0.15f, 0.08f), "Iron", Quaternion.Euler(0f, 0f, 90f), collider: false);

            // 쏟아지는 줄기 · 바닥 홈 · 고인 슬래그
            const float troughTop = 0.09f;
            var stream = Box("Slag Stream", root, new Vector3(sx, (sy + troughTop) / 2f, face - 0.26f), new Vector3(0.035f, sy - troughTop, 0.035f), "Slag", collider: false).transform;
            Box("Trough", root, new Vector3(sx, 0.04f, face - 0.3f), new Vector3(0.3f, 0.08f, 0.26f), "Stone");
            var puddle = Box("Slag Puddle", root, new Vector3(sx, 0.083f, face - 0.3f), new Vector3(0.24f, 0.01f, 0.2f), "Slag", collider: false).GetComponent<Renderer>();

            // 쇠꼬챙이: 끝이 배출구 앞에 오도록 왼쪽 앞에서 비스듬히(창을 가리지 않게). 축의 +z 가 배출구 쪽.
            var handleEnd = tip.position + new Vector3(-0.5f, 0.3f, -0.5f);
            var dir = (tip.position - handleEnd).normalized;
            var poker = new GameObject("Poker").transform;
            poker.SetParent(root, false);
            poker.SetPositionAndRotation(tip.position - dir * 0.1f, Quaternion.LookRotation(dir));
            Prim("Rod", PrimitiveType.Cylinder, poker, poker.position - dir * 0.45f, new Vector3(0.025f, 0.45f, 0.025f), "Iron", poker.rotation * Quaternion.Euler(90f, 0f, 0f), collider: false);
            Box("Grip", poker, poker.position - dir * 0.85f, new Vector3(0.04f, 0.04f, 0.12f), "Wood", collider: false).transform.rotation = poker.rotation;

            var cam = StationCam("Forge Slag", new Vector3(cx + 0.15f, 0.95f, face - 1.35f), new Vector3(cx - 0.05f, 0.4f, face - 0.1f));
            root.gameObject.AddComponent<SlagPit>().Bind(column, surface, poker, stream, puddle, tip, cam, H);
        }

        static ParticleSystem MakeEmbers(Transform parent, Vector3 at)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            main.startColor = new Color(1f, 0.5f, 0.1f) * 5f;
            main.gravityModifier = -0.08f;
            main.maxParticles = 300;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, 0.05f, 0.7f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats["Spark"];
            return ps;
        }

        static AnvilStation BuildAnvil(Vector3 at, GameObject room)
        {
            var cam = StationCam("Anvil", at + new Vector3(0.2f, 1.75f, -1.55f), at + new Vector3(0.1f, 0.95f, 0f));
            var s = (AnvilStation)Make<AnvilStation>("모루", at, room, StationRole.Work, StationKind.Anvil,
                at + new Vector3(0f, 0f, -1.1f), at + new Vector3(0.05f, 0.94f, 0f), cam);
            var t = s.transform;
            Prim("Stump", PrimitiveType.Cylinder, t, at + new Vector3(0f, 0.25f, 0f), new Vector3(0.7f, 0.25f, 0.7f), "Wood");
            Box("Anvil Base", t, at + new Vector3(0f, 0.58f, 0f), new Vector3(0.38f, 0.16f, 0.28f), "Iron");
            Box("Anvil Waist", t, at + new Vector3(0f, 0.72f, 0f), new Vector3(0.2f, 0.14f, 0.18f), "Iron");
            Box("Anvil Face", t, at + new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.12f, 0.26f), "Iron");
            Prim("Anvil Horn", PrimitiveType.Cylinder, t, at + new Vector3(0.42f, 0.86f, 0f), new Vector3(0.12f, 0.12f, 0.12f), "Iron", Quaternion.Euler(0f, 0f, 90f));

            // 망치: 축은 모루 왼쪽, 자루가 +x 로 뻗어 머리가 모루 면 위에 놓인다. 들면(+z 회전) 머리가 올라간다.
            var pivot = new GameObject("Hammer Pivot").transform;
            pivot.SetParent(t, false);
            pivot.position = at + new Vector3(-0.55f, 1.0f, -0.05f);
            Box("Hammer Shaft", pivot, pivot.position + new Vector3(0.3f, 0f, 0f), new Vector3(0.6f, 0.035f, 0.035f), "Wood", collider: false);
            Box("Hammer Head", pivot, pivot.position + new Vector3(0.6f, -0.02f, 0f), new Vector3(0.08f, 0.1f, 0.14f), "Iron", collider: false);

            // 힘 눈금 기둥: 모루 오른쪽 뒤. 금색 띠 = 필요한 힘, 흰 표시 = 지금 힘
            var post = new GameObject("Power Post").transform;
            post.SetParent(t, false);
            post.position = at + new Vector3(0.75f, 0.55f, 0.15f);
            Prim("Pole", PrimitiveType.Cylinder, post, post.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.04f, 0.4f, 0.04f), "Wood", collider: false);
            var band = Box("Target Band", post, post.position, new Vector3(0.1f, 0.1f, 0.1f), "Gold", collider: false).transform;
            var marker = Box("Power Marker", post, post.position, new Vector3(0.14f, 0.015f, 0.14f), "Marker", collider: false).transform;

            s.Bind(pivot, band, marker);
            return s;
        }

        static void BuildQuench(Vector3 at, GameObject room)
        {
            var cam = StationCam("Quench", at + new Vector3(0f, 1.8f, -1.5f), at + new Vector3(0f, 0.6f, 0f));
            var s = Make<Station>("물통", at, room, StationRole.Work, StationKind.Quench, at + new Vector3(0f, 0f, -1f), at + new Vector3(0f, 0.9f, 0f), cam);
            Prim("Barrel", PrimitiveType.Cylinder, s.transform, at + new Vector3(0f, 0.35f, 0f), new Vector3(0.8f, 0.35f, 0.8f), "Wood");
            Prim("Water", PrimitiveType.Cylinder, s.transform, at + new Vector3(0f, 0.71f, 0f), new Vector3(0.72f, 0.01f, 0.72f), "Water", collider: false);
            Prim("Oil Pot", PrimitiveType.Cylinder, s.transform, at + new Vector3(0.75f, 0.25f, 0.1f), new Vector3(0.45f, 0.25f, 0.45f), "Iron");
        }

        static void BuildGrind(Vector3 at, GameObject room)
        {
            var cam = StationCam("Grind", at + new Vector3(-0.6f, 1.8f, -1.5f), at + new Vector3(0f, 0.8f, 0f));
            var s = Make<Station>("숫돌", at, room, StationRole.Work, StationKind.Grind, at + new Vector3(-0.3f, 0f, -1.05f), at + new Vector3(0f, 1.1f, -0.2f), cam);
            Box("Frame L", s.transform, at + new Vector3(-0.25f, 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.5f), "Wood");
            Box("Frame R", s.transform, at + new Vector3(0.25f, 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.5f), "Wood");
            Prim("Wheel", PrimitiveType.Cylinder, s.transform, at + new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 0.08f, 0.8f), "Stone", Quaternion.Euler(0f, 0f, 90f));
            Box("Crank", s.transform, at + new Vector3(0.38f, 0.85f, 0f), new Vector3(0.04f, 0.04f, 0.3f), "Iron", collider: false);
        }

        static void BuildBench(Vector3 at, GameObject room)
        {
            var cam = StationCam("Bench", at + new Vector3(-1.2f, 1.9f, -0.9f), at + new Vector3(0f, 0.9f, 0f));
            var s = Make<Station>("작업대", at, room, StationRole.Work, StationKind.Bench, at + new Vector3(-1.4f, 0f, 0f), at + new Vector3(-0.4f, 0.92f, 0f), cam);
            Box("Top", s.transform, at + new Vector3(0f, 0.85f, 0f), new Vector3(1.8f, 0.08f, 0.9f), "Wood");
            foreach (var x in new[] { -0.8f, 0.8f })
                foreach (var z in new[] { -0.38f, 0.38f })
                    Box("Leg", s.transform, at + new Vector3(x, 0.41f, z), new Vector3(0.08f, 0.82f, 0.08f), "Wood");
            Box("Leather Roll", s.transform, at + new Vector3(0.3f, 0.95f, 0.15f), new Vector3(0.5f, 0.12f, 0.12f), "Leather", collider: false);
            Box("Grip Blanks", s.transform, at + new Vector3(-0.1f, 0.93f, -0.2f), new Vector3(0.25f, 0.08f, 0.08f), "Wood", collider: false);
        }

        static void BuildBoard(Vector3 at, GameObject room)
        {
            var s = Make<Station>("작업 지시서", at, room, StationRole.Board, StationKind.Bench, at + new Vector3(0f, 0f, -0.85f), at + new Vector3(0f, 1.5f, 0f), null);
            Box("Board", s.transform, at + new Vector3(0f, 1.55f, 0f), new Vector3(1.2f, 0.85f, 0.06f), "Wood");
            Box("Sheet", s.transform, at + new Vector3(-0.2f, 1.58f, -0.04f), new Vector3(0.4f, 0.5f, 0.01f), "Paper", collider: false);
            Box("Sheet 2", s.transform, at + new Vector3(0.28f, 1.52f, -0.04f), new Vector3(0.35f, 0.42f, 0.01f), "Paper", collider: false);
            // 걸어 다가갈 수 있게 바닥까지 닿는 기둥(클릭하기 쉽게)
            Box("Post", s.transform, at + new Vector3(0f, 0.55f, 0f), new Vector3(0.1f, 1.1f, 0.06f), "Wood");
        }

        static Transform BuildRack(Vector3 at, GameObject room)
        {
            var s = Make<Station>("진열대", at, room, StationRole.Rack, StationKind.Bench, at + new Vector3(-0.95f, 0f, 0f), at + new Vector3(0f, 1.5f, 0f), null);
            Box("Panel", s.transform, at + new Vector3(0f, 1.45f, 0f), new Vector3(0.06f, 1.1f, 2f), "Wood");
            Box("Stand", s.transform, at + new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 0.9f, 0.3f), "Wood");
            var pegs = new GameObject("Pegs").transform;
            pegs.SetParent(s.transform, false);
            for (int i = 0; i < 6; i++)
            {
                var peg = new GameObject($"Peg {i}").transform;
                peg.SetParent(pegs, false);
                peg.position = at + new Vector3(-0.08f, 1.45f, -0.8f + i * 0.32f);
                peg.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return pegs;
        }

        // ── 기본 도형 도우미 ─────────────────────────────

        static GameObject Box(string name, Transform parent, Vector3 worldPos, Vector3 scale, string mat, bool collider = true) =>
            Prim(name, PrimitiveType.Cube, parent, worldPos, scale, mat, null, collider);

        static GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 worldPos, Vector3 scale, string mat,
            Quaternion? rot = null, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, true);
            g.transform.position = worldPos;
            g.transform.rotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mats[mat];
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsureFolder(string dir)
        {
            dir = dir.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }
    }
}
