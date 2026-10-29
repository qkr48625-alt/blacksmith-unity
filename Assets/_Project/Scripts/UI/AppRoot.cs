using System;
using System.Collections.Generic;
using Blacksmith.Core;
using Blacksmith.Crafting;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Blacksmith.UI
{
    public interface IScreen
    {
        VisualElement Root { get; }
        void Tick(float dt);
    }

    // 앱 전체의 시작점. 저장 데이터를 들고, 화면(대장간 · 공정 · 완성)을 갈아 끼우고, 매 프레임 지금 화면에 Tick 을 준다.
    [RequireComponent(typeof(UIDocument))]
    public class AppRoot : MonoBehaviour
    {
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] List<RecipeDef> recipes = new();

        // 한글을 그릴 OS 글꼴 후보. 배포 전에는 Noto Sans KR 같은 OFL 글꼴을 프로젝트에 넣어 바꾼다.
        static readonly string[] KoreanFonts = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans KR", "Noto Sans CJK KR" };

        VisualElement _host;
        IScreen _screen;

        public SaveData Save { get; private set; }
        public IReadOnlyList<RecipeDef> Recipes => recipes;

        void Start()
        {
            Save = SaveSystem.Load();
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            root.AddToClassList("app");
            ApplyKoreanFont(root);
            _host = Ui.Box("app__host");
            root.Add(_host);
            ShowWorkshop();
        }

        void Update()
        {
            // 창을 끌거나 멈췄다 오면 dt 가 튀므로 0.1초로 자른다(웹 useFrame 과 같다).
            _screen?.Tick(Mathf.Min(Time.deltaTime, 0.1f));
        }

        public RecipeDef RecipeByKey(string key) => recipes.Find(r => r != null && r.key == key);

        public void Persist() => SaveSystem.Save(Save);

        public void ShowWorkshop() => Show(new WorkshopScreen(this));

        public void StartRun(RecipeDef recipe)
        {
            Save.run = new CraftRun
            {
                recipeKey = recipe.key,
                seed = Environment.TickCount ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            Save.hasRun = true;
            Persist();
            ContinueRun();
        }

        public void ContinueRun()
        {
            var recipe = Save.hasRun ? RecipeByKey(Save.run.recipeKey) : null;
            if (recipe == null)
            {
                AbandonRun();
                return;
            }
            // 마지막 공정까지 저장된 뒤 꺼졌으면 바로 완성으로 넘어간다
            if (Save.run.CurrentStageIndex(recipe) >= recipe.stages.Count)
            {
                CompleteRun(recipe);
                return;
            }
            Show(new ProcessScreen(this, recipe, Save.run));
        }

        public void AbandonRun()
        {
            Save.hasRun = false;
            Save.run = new CraftRun();
            Persist();
            ShowWorkshop();
        }

        // 마지막 공정이 끝났을 때. 기록을 보관함에 넣고 완성 화면을 띄운다.
        public void CompleteRun(RecipeDef recipe)
        {
            var run = Save.run;
            int total = Scoring.Total(recipe, run.stages);
            var record = new CraftRecord
            {
                recipeKey = recipe.key,
                recipeName = recipe.displayName,
                seed = run.seed,
                total = total,
                tierKey = Scoring.TierOf(total).Key,
                stages = run.stages,
                finishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            Save.collection.Add(record);
            Save.hasRun = false;
            Save.run = new CraftRun();
            Persist();
            Show(new ResultScreen(this, record, fresh: true));
        }

        public void ShowRecord(CraftRecord record) => Show(new ResultScreen(this, record, fresh: false));

        void Show(IScreen screen)
        {
            _host.Clear();
            _screen = screen;
            _host.Add(screen.Root);
        }

        static void ApplyKoreanFont(VisualElement root)
        {
            foreach (var family in KoreanFonts)
            {
                FontAsset font = null;
                try { font = FontAsset.CreateFontAsset(family, "Regular"); }
                catch (Exception) { /* 그 글꼴이 없으면 다음 후보 */ }
                if (font == null) continue;
                root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
                return;
            }
            Debug.LogWarning("[UI] 한글 OS 글꼴을 찾지 못했습니다. 한글이 네모로 보이면 Noto Sans KR 을 프로젝트에 넣으세요.");
        }
    }
}
