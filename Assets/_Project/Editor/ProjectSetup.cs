using System;
using System.Collections.Generic;
using System.IO;
using Blacksmith.Crafting;
using Blacksmith.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blacksmith.EditorTools
{
    // 프로젝트 뼈대를 만든다: PanelSettings, 공정 · 레시피 데이터, Main 씬, 빌드 씬 목록, 플레이어 설정.
    // 이미 있는 에셋은 건드리지 않으므로(인스펙터에서 고친 값이 지켜진다) 여러 번 돌려도 된다.
    // 배치 모드: Unity.exe -batchmode -quit -projectPath . -executeMethod Blacksmith.EditorTools.ProjectSetup.RunBatch
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string ThemePath = Root + "/UI/Theme.tss";
        const string StylePath = Root + "/UI/Main.uss";
        const string PanelPath = Root + "/UI/PanelSettings.asset";
        const string ProcessDir = Root + "/Data/Processes";
        const string RecipeDir = Root + "/Data/Recipes";
        const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Blacksmith/Setup Project")]
        public static void Run()
        {
            ConfigurePlayer();
            EnsurePanelSettings();
            EnsureDaggerRecipe();
            AssetDatabase.SaveAssets();
            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 완료");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "puha";
            PlayerSettings.productName = "대장장이 시뮬레이터";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
        }

        static PanelSettings EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel != null) return panel;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (panel.themeStyleSheet == null) throw new Exception($"테마를 찾지 못했습니다: {ThemePath}");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            CreateAsset(panel, PanelPath);
            return panel;
        }

        // 공정 정의. 글은 웹 버전 data/processes.js 에서 옮겼다. minigame 이 빈 단계는 임시 점수 입력으로 돈다.
        static RecipeDef EnsureDaggerRecipe()
        {
            var smelt = EnsureProcess("smelt", "제련", "광석 → 블룸 · 실제 약 1일",
                "숯과 광석을 노에 번갈아 넣고 풀무로 바람을 보냅니다. 1,100~1,200℃를 넘겨야 슬래그가 녹아 철과 분리됩니다. 녹은 슬래그는 노 아래 배출구를 뚫어 여러 번 흘려보냅니다.",
                "① 풀무질: 풀무 버튼(또는 스페이스)을 누르고 있으면 불이 세지고, 떼면 식습니다. 온도계의 금색 구간에 오래 머물수록 점수가 높아요. ② 슬래그 배출: 녹은 슬래그가 차오르면 배출구를 뚫어 흘려보냅니다.",
                ("bellows", "풀무질", 0.7f, "bellows"), ("slag", "슬래그 배출", 0.3f, ""));
            var fold = EnsureProcess("fold", "정련", "블룸 → 철 바 · 실제 약 반나절",
                "블룸을 백열 온도로 달궈 두드리고 반으로 접어 단접하기를 되풀이합니다. 접을수록 결이 고와지고 불순물이 빠집니다. 마지막으로 조각 끝을 부러뜨려 날감(강)과 몸통감(연철)을 가립니다.",
                "① 접기 단접: 버튼(또는 스페이스바)을 누르고 있으면 망치 힘이 차오릅니다. 금색 구간에 왔을 때 손을 떼면 제대로 맞고, 너무 오래 누르면 힘이 다시 빠집니다. 필요한 힘은 매번 달라집니다. 5번 접는 동안 20번 두드립니다. ② 재질 분류: 부러뜨린 조각의 단면을 보고 강과 연철을 빠르게 나눕니다.",
                ("fold", "접기 단접", 0.8f, "fold"), ("sort", "재질 분류", 0.2f, ""));
            var shape = EnsureProcess("shape", "칼날 단조", "철 바 → 칼날 형상 · 마크 · 불림 · 실제 약 1일",
                "마름모 단면과 날끝을 만듭니다. 달군 쇠를 망치로 쳐서 가운데 능선을 세우고, 끝으로 갈수록 송곳처럼 가늘게 늘립니다. 뜨거울 때 펀치로 제작자 마크를 찍고, 체리빛까지 데웠다 공기 중에 식히는 불림을 2~3번 해서 응력을 풉니다.",
                "① 형상: 떨리는 조준점을 빛나는 표적 위에 맞춰 클릭합니다. 칼이 식기 전에 노에 다시 넣으세요. ② 마크 각인: 펀치를 두 번 같은 자리에 칩니다. ③ 불림: 체리빛에서 떼고 검게 식을 때까지 기다리기를 3번.",
                ("form", "형상", 0.7f, ""), ("mark", "마크 각인", 0.15f, ""), ("normalize", "불림", 0.15f, ""));
            var temper = EnsureProcess("temper", "열처리", "담금질 · 뜨임 · 실제 약 반나절",
                "체리빛까지 달군 칼날을 물에 담가 단단하게 만들고(담금질), 다시 약하게 달궈 질긴 성질을 줍니다(뜨임).",
                "칼날 색만 보고 판단합니다. 1단계: 칼날이 체리빛이 될 때 담금 버튼. 2단계: 산화막 색이 목표색에 닿을 때 뜨임 버튼.",
                ("quench", "담금질", 0.5f, ""), ("temper", "뜨임", 0.5f, ""));
            var grind = EnsureProcess("grind", "연마", "숫돌 갈기 · 날 세우기 · 실제 약 1~2일",
                "크랭크로 돌리는 사암 숫돌에 칼날을 대고 날 선을 갈아 모양을 맞춥니다. 과열되면 뜨임이 풀리므로 물을 묻혀 가며 갑니다. 마지막에 손숫돌로 날을 세웁니다.",
                "① 숫돌 갈기: 숫돌 위를 누른 채 원을 그려 크랭크를 돌리고, 날이 달아오르면 물을 찍습니다. ② 날 세우기: 손숫돌을 뿌리에서 날끝까지 날 선을 따라 한 번에 밉니다.",
                ("grind", "숫돌 갈기", 0.7f, ""), ("hone", "날 세우기", 0.3f, ""));
            var handle = EnsureProcess("handle", "손잡이", "그립 구멍 · 조립 · 리벳 · 실제 약 1~2일",
                "그립 통에 송곳으로 슴베 구멍을 뚫고, 슴베에 그립과 론델(날밑)을 끼운 뒤 위로 튀어나온 슴베 끝을 망치로 두드려 퍼뜨려 고정합니다.",
                "① 그립 구멍: 송곳 끝이 옆으로 흐르지 않게 가운데 선을 지킵니다. ② 조립: 순서와 방향에 맞춰 슴베에 끼웁니다. ③ 리벳: 힘이 금색 구간에 왔을 때 떼서 5번 두드립니다.",
                ("awl", "그립 구멍", 0.25f, ""), ("assemble", "조립", 0.15f, ""), ("rivet", "리벳", 0.6f, ""));
            var sheath = EnsureProcess("sheath", "칼집", "가죽 성형 · 도장 · 바느질 · 실제 약 1일",
                "무두질한 송아지 가죽으로 칼을 감싸 모양을 잡고, 등 솔기를 꿰맵니다. 젖은 가죽에 도장을 찍어 장식을 넣습니다.",
                "① 가죽 성형: 가죽이 마르기 전에 칼 윤곽선을 따라 문질러 모양을 잡습니다. ② 도장 꾸미기: 좌우 대칭 · 고른 간격으로 도장을 찍습니다. ③ 리듬 바느질: 박자가 금색 선에 닿을 때 찌르기 · 당기기를 번갈아 누릅니다.",
                ("form", "가죽 성형", 0.3f, ""), ("stamp", "도장 꾸미기", 0.35f, ""), ("stitch", "리듬 바느질", 0.35f, ""));

            var path = RecipeDir + "/Dagger.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDef>(path);
            if (recipe != null) return recipe;
            recipe = ScriptableObject.CreateInstance<RecipeDef>();
            recipe.key = "dagger";
            recipe.displayName = "론델 단검";
            recipe.description = "15세기 론델 대거. 철광석에서 칼집까지 7공정을 거친다.";
            recipe.stages = new List<RecipeStage>
            {
                new() { process = smelt, weight = 0.12f },
                new() { process = fold, weight = 0.15f },
                new() { process = shape, weight = 0.23f },
                new() { process = temper, weight = 0.15f },
                new() { process = grind, weight = 0.15f },
                new() { process = handle, weight = 0.10f },
                new() { process = sheath, weight = 0.10f },
            };
            CreateAsset(recipe, path);
            return recipe;
        }

        static ProcessDef EnsureProcess(string key, string title, string real, string blurb, string controls,
            params (string key, string name, float weight, string minigame)[] parts)
        {
            var path = $"{ProcessDir}/{key}.asset";
            var def = AssetDatabase.LoadAssetAtPath<ProcessDef>(path);
            if (def != null) return def;
            def = ScriptableObject.CreateInstance<ProcessDef>();
            def.key = key;
            def.title = title;
            def.real = real;
            def.blurb = blurb;
            def.controls = controls;
            foreach (var p in parts)
                def.parts.Add(new PartDef { key = p.key, name = p.name, weight = p.weight, minigame = p.minigame });
            CreateAsset(def, path);
            return def;
        }

        static void EnsureScene()
        {
            bool exists = File.Exists(ScenePath);
            var scene = exists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 씬을 연 뒤에 읽는다. 씬을 열면 안 쓰는 에셋이 내려가서, 먼저 읽어 둔 참조는 빈 참조로 저장된다.
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            var dagger = AssetDatabase.LoadAssetAtPath<RecipeDef>(RecipeDir + "/Dagger.asset");
            if (panel == null || dagger == null) throw new Exception("PanelSettings 또는 단검 레시피를 읽지 못했습니다.");

            if (!exists)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(28, 25, 23, 255);
                cam.orthographic = true;
                new GameObject("App", typeof(UIDocument), typeof(AppRoot));
            }

            // 씬이 이미 있어도 비어 있는 참조만 채운다
            var app = UnityEngine.Object.FindAnyObjectByType<AppRoot>();
            if (app == null) throw new Exception($"{ScenePath} 에 AppRoot 가 없습니다.");
            var doc = app.GetComponent<UIDocument>();
            if (doc.panelSettings == null) doc.panelSettings = panel;
            var so = new SerializedObject(app);
            var style = so.FindProperty("styleSheet");
            if (style.objectReferenceValue == null)
                style.objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            var list = so.FindProperty("recipes");
            bool hasDagger = false;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var v = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (v == null) list.DeleteArrayElementAtIndex(i);
                else if (v == dagger) hasDagger = true;
            }
            if (!hasDagger)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = dagger;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(doc);

            EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void CreateAsset(UnityEngine.Object asset, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
        }

        static void EnsureFolder(string dir)
        {
            dir = dir.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }
    }
}
