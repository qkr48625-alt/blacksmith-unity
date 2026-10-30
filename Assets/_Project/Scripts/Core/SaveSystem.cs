using System;
using System.Collections.Generic;
using System.IO;
using Blacksmith.Crafting;
using UnityEngine;

namespace Blacksmith.Core
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        // JsonUtility 는 null 객체를 못 남겨서 진행 중인 한 자루가 있는지 따로 적는다.
        public bool hasRun;
        public CraftRun run = new();
        public List<CraftRecord> collection = new();
        // 과열로 버린 작업물 수
        public int ruined;
        // 접근성: 쇠 온도를 숫자로도 보여 준다(색 구분이 어려운 사람용, T 키)
        public bool showTemp;
    }

    // 저장 파일 하나(persistentDataPath 아래). 임시 파일에 쓴 뒤 바꿔 끼워 반쯤 쓰인 파일이 남지 않게 한다.
    // 1차 UI 화면과 3D 대장간은 진행 방식이 달라서 파일을 나눈다.
    public static class SaveSystem
    {
        public const string UiPrototypeFile = "blacksmith-save.json";
        public const string WorkshopFile = "blacksmith-workshop.json";

        static string PathOf(string file) => Path.Combine(Application.persistentDataPath, file);

        public static SaveData Load(string file = UiPrototypeFile)
        {
            var path = PathOf(file);
            try
            {
                if (!File.Exists(path)) return new SaveData();
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
                return new SaveData();
            }
        }

        public static void Save(SaveData data, string file = UiPrototypeFile)
        {
            var path = PathOf(file);
            try
            {
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e.Message}");
            }
        }
    }
}
