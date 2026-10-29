using UnityEditor;
using UnityEditor.SceneManagement;

namespace Blacksmith.EditorTools
{
    // 에디터를 열면 대장간 씬을 띄우고(빈 씬일 때만), ▶ 는 어느 씬에서 눌러도 대장간부터 시작하게 한다.
    [InitializeOnLoad]
    static class OpenWorkshopOnLoad
    {
        static OpenWorkshopOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                var start = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkshopSceneBuilder.ScenePath);
                if (start == null) return;
                EditorSceneManager.playModeStartScene = start;

                if (SessionState.GetBool("Blacksmith.OpenedWorkshop", false)) return;
                SessionState.SetBool("Blacksmith.OpenedWorkshop", true);
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(WorkshopSceneBuilder.ScenePath);
            };
        }
    }
}
