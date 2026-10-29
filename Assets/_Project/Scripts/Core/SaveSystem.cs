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
    }

    // 저장 파일 하나(persistentDataPath/blacksmith-save.json). 임시 파일에 쓴 뒤 바꿔 끼워 반쯤 쓰인 파일이 남지 않게 한다.
    public static class SaveSystem
    {
        const string FileName = "blacksmith-save.json";

        static string PathOf => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(PathOf)) return new SaveData();
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf));
                return data ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
                return new SaveData();
            }
        }

        public static void Save(SaveData data)
        {
            try
            {
                var tmp = PathOf + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(PathOf)) File.Replace(tmp, PathOf, null);
                else File.Move(tmp, PathOf);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e.Message}");
            }
        }
    }
}
