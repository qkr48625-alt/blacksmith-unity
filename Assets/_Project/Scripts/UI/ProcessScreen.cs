using Blacksmith.Crafting;
using Blacksmith.Minigames;
using UnityEngine.UIElements;

namespace Blacksmith.UI
{
    // 공정 틀: 소개 → 세부 단계 미니게임 → (단계 사이 점수) → 공정 점수 → 다음 공정.
    // 세부 단계가 끝날 때마다 저장해서, 껐다 켜면 다음 세부 단계부터 이어 한다(하던 단계 하나는 처음부터).
    public sealed class ProcessScreen : IScreen
    {
        enum Phase { Intro, Play, PartDone, StageDone }

        readonly AppRoot _app;
        readonly RecipeDef _recipe;
        readonly CraftRun _run;
        readonly VisualElement _stageChips, _partChips, _body;
        readonly Label _title, _real;

        int _stageIndex, _partIndex;
        Phase _phase;
        IMinigame _game;

        public VisualElement Root { get; }

        ProcessDef Process => _recipe.stages[_stageIndex].process;
        StageRecord Record => _run.stages[_stageIndex];

        public ProcessScreen(AppRoot app, RecipeDef recipe, CraftRun run)
        {
            _app = app;
            _recipe = recipe;
            _run = run;

            Root = Ui.Box("screen", "process");
            var top = Ui.Row("space-between");
            _stageChips = Ui.Row("chips");
            top.Add(_stageChips);
            top.Add(Ui.Button("대장간으로", app.ShowWorkshop, "btn-small"));
            Root.Add(top);
            var titleRow = Ui.Row("title-row");
            _title = Ui.Label("", "h1");
            _real = Ui.Label("", "muted");
            titleRow.Add(_title);
            titleRow.Add(_real);
            Root.Add(titleRow);
            _partChips = Ui.Row("chips");
            Root.Add(_partChips);
            _body = Ui.Box("process__body");
            Root.Add(_body);

            EnterStage(run.CurrentStageIndex(recipe));
        }

        void EnterStage(int index)
        {
            _stageIndex = index;
            if (_stageIndex >= _recipe.stages.Count)
            {
                _app.CompleteRun(_recipe);
                return;
            }
            if (_run.stages.Count <= _stageIndex) _run.stages.Add(new StageRecord { key = Process.key });
            _partIndex = Record.parts.Count;
            _title.text = Process.title;
            _real.text = Process.real;
            SetPhase(Phase.Intro);
        }

        public void Tick(float dt)
        {
            if (_phase != Phase.Play || _game == null) return;
            _game.Tick(dt);
            if (!_game.IsDone) return;

            var part = Process.parts[_partIndex];
            Record.parts.Add(new PartRecord { key = part.key, score = _game.Result.Score, detail = _game.Result.Detail });
            _game = null;
            bool stageDone = Record.parts.Count >= Process.parts.Count;
            if (stageDone) Record.score = Scoring.PartsScore(Process, Record);
            _app.Persist();
            SetPhase(stageDone ? Phase.StageDone : Phase.PartDone);
        }

        void SetPhase(Phase phase)
        {
            _phase = phase;
            RenderChips();
            _body.Clear();
            switch (phase)
            {
                case Phase.Intro: RenderIntro(); break;
                case Phase.Play: StartPart(); break;
                case Phase.PartDone: RenderPartDone(); break;
                case Phase.StageDone: RenderStageDone(); break;
            }
        }

        void RenderChips()
        {
            _stageChips.Clear();
            for (int i = 0; i < _recipe.stages.Count; i++)
            {
                var chip = Ui.Label($"{i + 1}. {_recipe.stages[i].process.title}", "chip");
                chip.EnableInClassList("chip--done", i < _stageIndex);
                chip.EnableInClassList("chip--current", i == _stageIndex);
                _stageChips.Add(chip);
            }

            _partChips.Clear();
            for (int i = 0; i < Process.parts.Count; i++)
            {
                var p = Process.parts[i];
                var rec = Record.Part(p.key);
                var chip = Ui.Label(rec != null ? $"{i + 1}. {p.name} · {rec.score}점" : $"{i + 1}. {p.name}", "chip", "chip--part");
                chip.EnableInClassList("chip--done", rec != null);
                chip.EnableInClassList("chip--current", rec == null && i == _partIndex);
                _partChips.Add(chip);
            }
        }

        void RenderIntro()
        {
            var panel = Ui.Box("panel", "intro");
            panel.Add(Ui.Label(Process.blurb, "intro__blurb"));
            panel.Add(Ui.Label("조작", "small", "amber"));
            panel.Add(Ui.Label(Process.controls, "intro__controls"));
            string label = _partIndex > 0 ? $"이어 하기 — {Process.parts[_partIndex].name}부터" : "시작";
            panel.Add(Ui.Button(label, () => SetPhase(Phase.Play), "btn-primary", "btn-large"));
            _body.Add(panel);
        }

        void StartPart()
        {
            var part = Process.parts[_partIndex];
            int seed = _run.seed ^ ((_stageIndex * 31 + _partIndex + 1) * unchecked((int)0x9E3779B1));
            _game = MinigameRegistry.Create(part.minigame, part.name);
            _game.Build(_body, new MinigameContext(seed));
        }

        void RenderPartDone()
        {
            var done = Process.parts[_partIndex];
            var next = Process.parts[_partIndex + 1];
            var panel = Ui.Box("panel", "between");
            panel.Add(Ui.Label($"{done.name} 끝", "muted"));
            panel.Add(Ui.Label($"{Record.Part(done.key).score}점", "big-number", "amber"));
            panel.Add(Ui.Label($"다음 단계: {_partIndex + 2}. {next.name}", "h2"));
            panel.Add(Ui.Button("다음 단계 시작", () =>
            {
                _partIndex++;
                SetPhase(Phase.Play);
            }, "btn-primary", "btn-large"));
            _body.Add(panel);
        }

        void RenderStageDone()
        {
            var panel = Ui.Box("panel", "between");
            panel.Add(Ui.Label($"{Process.title} 완료", "muted"));
            panel.Add(Ui.Label($"{Record.score}점", "big-number", "amber"));
            foreach (var p in Process.parts)
            {
                var row = Ui.Row("score-row");
                row.Add(Ui.Label(p.name, "grow"));
                row.Add(Ui.Label($"{p.weight * 100:0}%", "muted", "score-row__weight"));
                row.Add(Ui.Label($"{Record.Part(p.key)?.score ?? 0}점", "score-row__score"));
                panel.Add(row);
            }
            bool last = _stageIndex + 1 >= _recipe.stages.Count;
            string label = last ? "완성품 보기" : $"다음 공정: {_recipe.stages[_stageIndex + 1].process.title}";
            panel.Add(Ui.Button(label, () => EnterStage(_stageIndex + 1), "btn-primary", "btn-large"));
            _body.Add(panel);
        }
    }
}
