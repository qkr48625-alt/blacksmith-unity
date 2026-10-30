using System.IO;
using System.Text;
using Blacksmith.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blacksmith.EditorTools
{
    // 대장간 씬을 플레이 모드로 돌려 보는 자동 점검. 에러 · 길(NavMesh) · W 키 이동을 확인하고,
    // 카메라 화면을 Logs/smoke-*.png 로 찍는다. 결과는 Logs/smoke.txt.
    // 배치 모드: Unity.exe -batchmode -projectPath . -executeMethod Blacksmith.EditorTools.PlaySmokeTest.Run
    // (-nographics 를 붙이면 사진이 안 찍힌다)
    [InitializeOnLoad]
    static class PlaySmokeTest
    {
        const string SessionKey = "Blacksmith.Smoke";
        const string OutDir = "Logs";

        static int _frame;
        static Vector3 _startPos;
        static readonly StringBuilder Report = new();

        [MenuItem("Blacksmith/Play Smoke Test")]
        public static void Run()
        {
            SessionState.SetInt(SessionKey, 1);
            EditorSceneManager.OpenScene(WorkshopSceneBuilder.ScenePath);
            EditorApplication.EnterPlaymode();
        }

        static PlaySmokeTest()
        {
            if (SessionState.GetInt(SessionKey, 0) == 0) return;
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error)
                    File.AppendAllText(Path.Combine(OutDir, "smoke.txt"), $"[{type}] {msg}\n{stack}\n");
            };
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            _frame++;
            var player = Object.FindAnyObjectByType<PlayerController>();
            var agent = player != null ? player.GetComponent<NavMeshAgent>() : null;

            if (_frame == 30)
            {
                File.WriteAllText(Path.Combine(OutDir, "smoke.txt"), "");
                var tri = NavMesh.CalculateTriangulation();
                Line($"navmesh triangles={tri.indices.Length / 3}");
                Line($"player found={player != null} agentEnabled={agent != null && agent.enabled} onNavMesh={agent != null && agent.isOnNavMesh} pos={player?.transform.position}");
                Line($"body local={player?.transform.Find("Body")?.localPosition} (x·z 가 0 이어야 제자리에서 돈다)");
                var markers = Object.FindAnyObjectByType<QuestMarkers>();
                Line($"quest markers={(markers != null ? markers.transform.childCount : -1)} ({(markers != null && markers.transform.childCount > 0 ? markers.transform.GetChild(0).name : "")})");
                Line($"keyboard={Keyboard.current != null} mouse={Mouse.current != null}");
                Capture("smoke-start.png");
                // 슬래그 단면 창: 차오른 상태 + 쏟아지는 줄기를 그 카메라 자리에서 찍는다
                var pit = Object.FindAnyObjectByType<Blacksmith.Stations.SlagPit>();
                Line($"slag pit={pit != null} dial band pivot={GameObject.Find("Band Pivot") != null}");
                if (pit != null)
                {
                    pit.SetLevel(0.62f);
                    pit.SetPouring(true);
                    CaptureFrom("CM Forge Slag", "smoke-slag.png");
                    pit.SetPouring(false);
                }
                // 화덕 시점(풀무질 · 온도계)
                CaptureFrom("CM Forge", "smoke-forge.png");
                // 창 없는 Unity 는 앱 포커스가 없어서, 포커스를 무시해야 가상 키보드 입력이 들어간다
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>().MakeCurrent();
                Line($"virtual keyboard={Keyboard.current != null}");
                _startPos = player != null ? player.transform.position : Vector3.zero;
            }
            // 40~100 프레임 동안 W 를 누르고 있는다
            if (_frame >= 40 && _frame < 100 && Keyboard.current != null)
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
            if (_frame == 100 && Keyboard.current != null)
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            if (_frame == 110)
            {
                var pos = player != null ? player.transform.position : Vector3.zero;
                Line($"after W: pos={pos} moved={(pos - _startPos).magnitude:0.00}m yaw={player?.transform.eulerAngles.y:0} nearest={player?.Nearest?.DisplayName}");
                Capture("smoke-after-move.png");
            }
            if (_frame == 120)
            {
                File.AppendAllText(Path.Combine(OutDir, "smoke.txt"), Report.ToString());
                SessionState.EraseInt(SessionKey);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
        }

        static void Line(string s)
        {
            Report.AppendLine(s);
            Debug.Log($"[Smoke] {s}");
        }

        // 이름으로 찾은 Cinemachine 카메라 자리에서 찍는다(블렌드를 기다리지 않게 임시 카메라를 둔다)
        static void CaptureFrom(string cmName, string file)
        {
            var cm = System.Array.Find(Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(), c => c.name == cmName);
            if (cm == null)
            {
                Line($"capture skipped ({file}: {cmName} 없음)");
                return;
            }
            var temp = new GameObject("Smoke Cam").AddComponent<Camera>();
            temp.transform.SetPositionAndRotation(cm.transform.position, cm.transform.rotation);
            temp.fieldOfView = cm.Lens.FieldOfView;
            temp.nearClipPlane = 0.05f;
            temp.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Capture(file, temp);
            Object.DestroyImmediate(temp.gameObject);
        }

        // 메인 카메라 화면을 PNG 로(HUD 는 화면 위 UI 라 빠진다)
        static void Capture(string file, Camera cam = null)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Line($"capture skipped ({file})");
                return;
            }
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
            // 평균 밝기(0~1)
            float sum = 0f;
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i += 97) sum += (px[i].r + px[i].g + px[i].b) / (3f * 255f);
            Line($"{file} brightness={sum / (px.Length / 97f):0.000}");
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
