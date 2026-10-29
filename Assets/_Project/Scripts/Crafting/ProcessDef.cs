using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blacksmith.Crafting
{
    // 대장간의 작업대 종류. 세부 단계마다 어느 작업대에서 하는지 정한다.
    public enum StationKind
    {
        Forge,  // 화덕
        Anvil,  // 모루
        Quench, // 물통 · 기름통
        Grind,  // 숫돌
        Bench,  // 작업대(손잡이 · 칼집)
    }

    // 공정 안의 세부 단계. 공정 점수 = 세부 단계 점수 × weight 의 합(공정마다 weight 합계 1).
    [Serializable]
    public class PartDef
    {
        public string key;
        public string name;
        [Range(0f, 1f)] public float weight;
        // 미니게임 키. 3D 대장간에서는 ActivityRegistry, 1차 UI 화면에서는 MinigameRegistry 가 쓴다.
        // 모르는 키면 임시 점수 입력으로 대신한다.
        public string minigame;
        public StationKind station;
        // 먼저 끝나 있어야 하는 세부 단계("공정키.단계키"). 비어 있으면 처음부터 할 수 있다.
        public List<string> requires = new();
        // 집게로 작업물을 들고 있어야 시작할 수 있는가
        public bool needsWorkpiece;
        // 끝나면 작업물이 이 상태가 된다(WorkpieceState 이름). 비어 있으면 그대로.
        public string producesState;
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
        // 셋업 스크립트가 새 필드(작업대 · 선행 관계 …)를 채운 판. 사람이 고친 값을 다시 덮지 않으려고 둔다.
        [HideInInspector] public int metaVersion;

        public PartDef Part(string partKey) => parts.Find(p => p.key == partKey);
    }
}
