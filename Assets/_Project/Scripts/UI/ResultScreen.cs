using Blacksmith.Crafting;
using UnityEngine.UIElements;

namespace Blacksmith.UI
{
    // 완성 화면 · 보관함 기록 보기: 등급, 능력치, 공정별 점수와 세부 단계 측정값.
    public sealed class ResultScreen : IScreen
    {
        public VisualElement Root { get; }

        public ResultScreen(AppRoot app, CraftRecord record, bool fresh)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen");
            Root = scroll;

            var recipe = app.RecipeByKey(record.recipeKey);
            var tier = Scoring.TierByKey(record.tierKey);

            var head = Ui.Row("space-between");
            head.Add(Ui.Label(fresh ? $"{record.recipeName} 완성!" : $"{record.recipeName} · {record.finishedAt}", "muted"));
            head.Add(Ui.Button("대장간으로", app.ShowWorkshop, "btn-small"));
            scroll.Add(head);

            var hero = Ui.Box("panel", "hero");
            hero.Add(Ui.Label(tier.Name ?? record.tierKey, "hero__tier", "amber"));
            hero.Add(Ui.Label($"{record.total}점", "big-number"));
            hero.Add(Ui.Label(tier.Blurb ?? "", "muted"));
            scroll.Add(hero);

            var stats = Scoring.StatsOf(record.stages);
            var statRow = Ui.Row("stats");
            AddStat(statRow, "예리함", stats.Sharpness);
            AddStat(statRow, "강도", stats.Durability);
            AddStat(statRow, "장식미", stats.Beauty);
            AddStat(statRow, "공격력", stats.Attack);
            scroll.Add(statRow);

            scroll.Add(Ui.Label("공정별 점수", "h2", "section"));
            for (int i = 0; i < record.stages.Count; i++)
            {
                var st = record.stages[i];
                var def = recipe != null && i < recipe.stages.Count ? recipe.stages[i] : null;
                var panel = Ui.Box("panel", "stage-card");
                var row = Ui.Row("score-row");
                row.Add(Ui.Label(def?.process.title ?? st.key, "h2", "grow"));
                if (def != null) row.Add(Ui.Label($"비중 {def.weight * 100:0}%", "muted", "score-row__weight"));
                row.Add(Ui.Label($"{st.score}점", "score-row__score", "amber"));
                panel.Add(row);

                foreach (var part in st.parts)
                {
                    var partDef = def?.process.Part(part.key);
                    var pr = Ui.Row("score-row", "score-row--part");
                    pr.Add(Ui.Label(partDef?.name ?? part.key, "grow"));
                    if (partDef != null) pr.Add(Ui.Label($"{partDef.weight * 100:0}%", "muted", "score-row__weight"));
                    pr.Add(Ui.Label($"{part.score}점", "score-row__score"));
                    panel.Add(pr);
                    foreach (var d in part.detail)
                        panel.Add(Ui.Label($"· {d.label}: {d.value}", "tiny", "muted", "detail"));
                }
                scroll.Add(panel);
            }
        }

        static void AddStat(VisualElement row, string name, int value)
        {
            var tile = Ui.Box("panel", "stat");
            tile.Add(Ui.Label(name, "small", "muted"));
            tile.Add(Ui.Label(value.ToString(), "stat__value"));
            row.Add(tile);
        }

        public void Tick(float dt) { }
    }
}
