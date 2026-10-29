using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blacksmith.EditorTools
{
    // URP 파이프라인 에셋 · 렌더러 · 후처리 프로필을 만들고 그래픽 · 품질 설정에 건다. 이미 있으면 그대로 쓴다.
    public static class RenderSetup
    {
        const string Dir = ProjectSetup.Root + "/Settings";
        const string RendererPath = Dir + "/URP_Renderer.asset";
        const string PipelinePath = Dir + "/URP_Pipeline.asset";
        public const string VolumePath = Dir + "/Workshop_Volume.asset";

        public static void Ensure()
        {
            WorkshopSceneBuilder.EnsureFolder(Dir);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = true;
                pipeline.shadowDistance = 30f;
                pipeline.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            }

            // 코드로 만든 렌더러는 후처리 데이터가 비어 있어 Bloom 등이 꺼진다. 패키지 기본값을 넣는다.
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData != null && rendererData.postProcessData == null)
            {
                rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            EnsureVolumeProfile();
        }

        // 달군 쇠 · 숯이 빛나 보이게 Bloom, 어두운 대장간 느낌으로 비네트 · 톤 매핑
        static void EnsureVolumeProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath) != null) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.65f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.3f);
            color.contrast.Override(8f);

            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
    }
}
