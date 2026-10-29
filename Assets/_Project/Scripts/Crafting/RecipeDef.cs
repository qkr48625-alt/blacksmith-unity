using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blacksmith.Crafting
{
    [Serializable]
    public class RecipeStage
    {
        public ProcessDef process;
        // 최종 점수에서 이 공정이 차지하는 비중. 레시피마다 합계 1.
        [Range(0f, 1f)] public float weight;
    }

    // 무기 하나를 만드는 공정 순서. 공정은 공용이고 순서와 가중치만 레시피마다 다르다.
    [CreateAssetMenu(menuName = "Blacksmith/Recipe", fileName = "Recipe")]
    public class RecipeDef : ScriptableObject
    {
        public string key;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public List<RecipeStage> stages = new();
    }
}
