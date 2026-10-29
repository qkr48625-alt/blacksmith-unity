using System.Linq;
using Blacksmith.Crafting;
using UnityEngine.UIElements;

namespace Blacksmith.UI
{
    // 대장간 입구: 만들던 물건 이어 하기, 레시피 고르기, 보관함.
    public sealed class WorkshopScreen : IScreen
    {
        public VisualElement Root { get; }

        public WorkshopScreen(AppRoot app)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen");
            Root = scroll;

            scroll.Add(Ui.Label("대장간", "h1"));
            scroll.Add(Ui.Label("만들 물건을 고르세요. 지금은 풀무질 · 접기 단접만 미니게임이고, 나머지 단계는 임시 점수로 넘어갑니다.", "muted"));

            var save = app.Save;
            if (save.hasRun)
            {
                var recipe = app.RecipeByKey(save.run.recipeKey);
                if (recipe != null)
                {
                    int idx = save.run.CurrentStageIndex(recipe);
                    var panel = Ui.Box("panel", "card", "card--active");
                    panel.Add(Ui.Label("만들던 물건", "small", "amber"));
                    panel.Add(Ui.Label(recipe.displayName, "h2"));
                    string at = idx < recipe.stages.Count ? recipe.stages[idx].process.title : "마무리";
                    panel.Add(Ui.Label($"{idx + 1}/{recipe.stages.Count} 공정 · {at} · 시작 {save.run.startedAt}", "muted"));
                    var row = Ui.Row("gap");
                    row.Add(Ui.Button("이어 하기", app.ContinueRun, "btn-primary"));
                    row.Add(Ui.Button("버리기", app.AbandonRun));
                    panel.Add(row);
                    scroll.Add(panel);
                }
            }

            scroll.Add(Ui.Label("레시피", "h2", "section"));
            foreach (var recipe in app.Recipes.Where(r => r != null))
            {
                var card = Ui.Box("panel", "card");
                card.Add(Ui.Label(recipe.displayName, "h2"));
                card.Add(Ui.Label(recipe.description, "muted"));
                card.Add(Ui.Label(string.Join(" → ", recipe.stages.Select(s => s.process.title)), "small"));
                var r = recipe;
                card.Add(Ui.Button(save.hasRun ? "새로 만들기 (만들던 것은 버림)" : "만들기", () => app.StartRun(r), "btn-primary"));
                scroll.Add(card);
            }

            scroll.Add(Ui.Label($"보관함 ({save.collection.Count})", "h2", "section"));
            if (save.collection.Count == 0) scroll.Add(Ui.Label("아직 완성한 물건이 없습니다.", "muted"));
            foreach (var rec in Enumerable.Reverse(save.collection))
            {
                var tier = Scoring.TierByKey(rec.tierKey);
                var row = Ui.Row("panel", "record");
                row.Add(Ui.Label(tier.Name ?? rec.tierKey, "record__tier", "amber"));
                row.Add(Ui.Label($"{rec.total}점", "record__total"));
                row.Add(Ui.Label($"{rec.recipeName} · {rec.finishedAt}", "muted", "grow"));
                var record = rec;
                row.Add(Ui.Button("보기", () => app.ShowRecord(record)));
                scroll.Add(row);
            }
        }

        public void Tick(float dt) { }
    }
}
