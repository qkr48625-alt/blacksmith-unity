using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Blacksmith.EditorTools
{
    // 점검용 자동 시작: 프로젝트의 Temp/blacksmith-autoplay.txt 에 세부 단계 id(예: fold.fold)가 있으면,
    // 에디터가 켜질 때 대장간 씬을 열고 ▶ 를 누른다. 그 단계로 데려가는 건 실행 중의 WorkshopGame 이 파일을 읽어 한다.
    [InitializeOnLoad]
    static class AutoPlayLauncher
    {
        public const string TriggerFile = "Temp/blacksmith-autoplay.txt";

        static AutoPlayLauncher()
        {
            if (!File.Exists(TriggerFile) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EditorSceneManager.OpenScene(WorkshopSceneBuilder.ScenePath);
                EditorApplication.EnterPlaymode();
            };
        }
    }
}
