using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Blacksmith.EditorTools
{
    // 점검용 자동 시작: 프로젝트의 Temp/blacksmith-autoplay.txt 에 세부 단계 id(예: fold.fold)가 생기면
    // 대장간 씬을 열고 ▶ 를 누른다(에디터를 켤 때도, 켜 둔 채로도 1초마다 살핀다).
    // 그 단계로 데려가는 건 실행 중의 WorkshopGame 이 파일을 읽어 한다.
    [InitializeOnLoad]
    static class AutoPlayLauncher
    {
        public const string TriggerFile = "Temp/blacksmith-autoplay.txt";
        static double _next;

        static AutoPlayLauncher() => EditorApplication.update += Poll;

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(TriggerFile) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (EditorSceneManager.GetActiveScene().path != WorkshopSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(WorkshopSceneBuilder.ScenePath);
            }
            EditorApplication.EnterPlaymode();
        }
    }
}
