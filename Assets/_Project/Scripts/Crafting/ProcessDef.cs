using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blacksmith.Crafting
{
    // 공정 안의 세부 단계. 공정 점수 = 세부 단계 점수 × weight 의 합(공정마다 weight 합계 1).
    [Serializable]
    public class PartDef
    {
        public string key;
        public string name;
        [Range(0f, 1f)] public float weight;
        // MinigameRegistry 에 등록된 미니게임 키. 없거나 모르는 키면 임시 점수 입력으로 대신한다.
        public string minigame;
    }

    // 공정 하나(제련 · 정련 …). 여러 레시피가 같은 공정을 함께 쓴다.
    [CreateAssetMenu(menuName = "Blacksmith/Process", fileName = "Process")]
    public class ProcessDef : ScriptableObject
    {
        public string key;
        public string title;
        public string real;
        [TextArea(3, 6)] public string blurb;
        [TextArea(3, 8)] public string controls;
        public List<PartDef> parts = new();

        public PartDef Part(string partKey) => parts.Find(p => p.key == partKey);
    }
}
