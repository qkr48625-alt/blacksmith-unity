using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith.Activities;
using Blacksmith.Core;
using Blacksmith.Crafting;
using Blacksmith.Feedback;
using Blacksmith.Minigames;
using Blacksmith.Pieces;
using Blacksmith.Stations;
using Blacksmith.UI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Blacksmith.World
{
    // 3D 대장간의 진행을 맡는다: 저장, 작업 지시서(선행 관계), 작업대 들어가고 나오기, 활동 시작 · 이어 하기 · 기록,
    // 작업물(온도 · 과열 · 버리기), 완성 → 진열대.
    [RequireComponent(typeof(UIDocument))]
    public class WorkshopGame : MonoBehaviour
    {
        // 과열: 탄 시간 1초마다 점수 -4, 모두 6초를 넘으면 작업물을 버린다. 3초에서 한 번 더 경고.
        const float BurnPenaltyPerSecond = 4f;
        const float BurnRuinSeconds = 6f;
        const float BurnLastWarning = 3f;
        // 제련을 막 끝낸 블룸의 온도
        const float FreshBloomTemp = 1150f;

        public static bool PointerOverUi { get; private set; }

        [SerializeField] List<RecipeDef> recipes = new();
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] PlayerController player;
        [SerializeField] NavMeshSurface navSurface;
        [SerializeField] Material metalMaterial;
        [SerializeField] Transform rack;          // 진열대 걸이(자식마다 한 자루)

        SaveData _save;
        Hud _hud;
        Station[] _stations;
        Station _current;
        int _enteredFrame;
        IStationActivity _activity;
        readonly Dictionary<string, IStationActivity> _inProgress = new();
        Workpiece _piece;
        ForgeStation _reheatForge;
        bool _burnWarnedFirst, _burnWarnedLast;

        RecipeDef Recipe => _save.hasRun ? recipes.Find(r => r != null && r.key == _save.run.recipeKey) : null;

        public void Bind(List<RecipeDef> recipeList, StyleSheet sheet, PlayerController p, NavMeshSurface surface, Material metal, Transform rackRoot)
        {
            recipes = recipeList;
            styleSheet = sheet;
            player = p;
            navSurface = surface;
            metalMaterial = metal;
            rack = rackRoot;
        }

        void Awake()
        {
#if UNITY_EDITOR
            // 에디터 기본값은 Game 창에 포커스가 있을 때만 키보드가 게임으로 간다.
            // ▶ 뒤에 다른 창을 눌러 두면 WASD 가 안 먹어서, 플레이 중에는 늘 게임으로 보낸다.
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        }

        void Start()
        {
            _save = SaveSystem.Load(SaveSystem.WorkshopFile);

            var root = GetComponent<UIDocument>().rootVisualElement;
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            AppRoot.ApplyKoreanFont(root);
            _hud = new Hud(root);

            _stations = FindObjectsByType<Station>();

            // 바닥 길을 실행할 때 굽는다(작은 방이라 순식간이고, 작업대를 옮겨도 다시 구울 필요가 없다)
            navSurface.BuildNavMesh();
            var agent = player.GetComponent<NavMeshAgent>();
            agent.enabled = true;
            agent.Warp(player.transform.position);
            player.Init(_stations);
            player.InteractRequested += EnterStation;

            if (_save.hasRun && Recipe == null) ResetRun();
            if (_save.hasRun && Enum.TryParse(_save.run.workpieceState, out WorkpieceState st) && st != WorkpieceState.None)
                SpawnPiece(st, Workpiece.RoomTemp, _save.run.burnTotal);

            BuildRack();
            RefreshOrder();
            if (!_save.hasRun) _hud.Toast("작업 지시서 게시판에서 만들 물건을 고르세요");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var mouse = Mouse.current;
            PointerOverUi = mouse != null && _hud.IsPointerOver(mouse.position.ReadValue());
            _hud.Tick(dt);
            player.Locked = _current != null || _hud.OverlayOpen;

            var kb = Keyboard.current;
            bool leave = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame);
            if (leave && Time.frameCount != _enteredFrame)
            {
                if (_hud.OverlayOpen) CloseOverlay();
                else if (_current != null) ExitStation();
            }

            if (_activity != null)
            {
                _activity.Tick(dt);
                if (_activity != null && _activity.IsDone) CompletePart(_activity);
                else if (_activity != null) _hud.SetHint(_activity.Hint);
            }
            else if (_reheatForge != null && _piece != null)
            {
                _hud.SetHint($"풀무질로 불을 키워 쇠를 달구세요 · 쇠: {HeatWord(_piece.Temperature)} · E/Esc 로 꺼내기");
            }

            UpdatePrompt();
            UpdateBurn();
        }

        void OnApplicationQuit()
        {
            if (_save.hasRun && _piece != null) _save.run.burnTotal = _piece.BurnTotal;
            Persist();
        }

        void Persist() => SaveSystem.Save(_save, SaveSystem.WorkshopFile);

        // ── 작업대 ──────────────────────────────────────────

        void EnterStation(Station s)
        {
            if (_current != null || _hud.OverlayOpen) return;
            _current = s;
            _enteredFrame = Time.frameCount;
            player.Stop();
            player.FaceTowards(s.WorkPoint.position);
            s.Enter();
            _hud.SetPrompt(null);

            switch (s.Role)
            {
                case StationRole.Board: ShowBoard(); break;
                case StationRole.Rack: ShowRack(); break;
                default: ShowStationOptions(); break;
            }
        }

        void ExitStation()
        {
            if (_current == null) return;
            if (_activity != null)
            {
                _activity.Exit();
                _activity = null;
            }
            StopReheat();
            HoldPiece();
            _current.Exit();
            _current = null;
            _hud.ClearOptions();
            _hud.SetHint(null);
            RefreshOrder();
        }

        void ShowStationOptions()
        {
            _hud.ClearOptions();
            var recipe = Recipe;
            if (recipe == null)
            {
                _hud.Toast("먼저 작업 지시서 게시판에서 만들 물건을 고르세요");
                ExitStation();
                return;
            }

            // 이 작업대에서 하던 일이 있으면 바로 이어 한다
            var resume = _inProgress.Values.FirstOrDefault(a => a.Part.Part.station == _current.Kind);
            if (resume != null)
            {
                StartActivity(resume.Part);
                return;
            }

            var options = new List<(string label, Action act, bool enabled)>();
            foreach (var p in CraftPlan.Available(recipe, _save.run).Where(p => p.Part.station == _current.Kind))
            {
                var part = p;
                bool ok = !p.Part.needsWorkpiece || _piece != null;
                options.Add((ok ? $"{p.Part.name} 시작" : $"{p.Part.name} — 작업물을 들고 오세요", () => StartActivity(part), ok));
            }
            if (_current is ForgeStation && _piece != null) options.Add(("쇠 달구기", StartReheat, true));

            if (options.Count == 0)
            {
                _hud.Toast($"{_current.DisplayName}: 지금 할 일이 없습니다");
                ExitStation();
                return;
            }
            if (options.Count == 1 && options[0].enabled)
            {
                options[0].act();
                return;
            }
            foreach (var (label, act, enabled) in options) _hud.AddOption(label, act, enabled, primary: enabled);
            _hud.SetHint("할 일을 고르세요 · Esc 로 떠나기");
        }

        void StartActivity(PartRef p)
        {
            _hud.ClearOptions();
            StopReheat();
            if (!_inProgress.TryGetValue(p.Id, out var act))
            {
                act = ActivityRegistry.Create(p);
                int seed = _save.run.seed ^ p.Id.GetHashCode();
                act.Begin(new ActivityContext { Station = _current, Piece = _piece, Game = new MinigameContext(seed), Hud = _hud });
                _inProgress[p.Id] = act;
            }
            if (p.Part.needsWorkpiece) PlacePiece(_current.WorkPoint);
            _activity = act;
            act.Enter();
            _hud.SetHint(act.Hint);
            RefreshOrder();
        }

        void StartReheat()
        {
            if (!(_current is ForgeStation forge) || _piece == null) return;
            _hud.ClearOptions();
            PlacePiece(forge.WorkPoint);
            _piece.SetFire(() => forge.FireTemp);
            _reheatForge = forge;
        }

        void StopReheat()
        {
            if (_reheatForge == null) return;
            _piece?.SetFire(null);
            _reheatForge = null;
        }

        void CompletePart(IStationActivity act)
        {
            var p = act.Part;
            var result = act.Result;
            _inProgress.Remove(p.Id);
            _activity = null;

            // 그동안 탄 시간만큼 이 단계 점수를 깎는다
            int score = result.Score;
            float burn = _piece != null ? _piece.TakeBurnMark() : 0f;
            var detail = new List<DetailEntry>(result.Detail);
            if (burn > 0f)
            {
                int penalty = Scoring.Round(burn * BurnPenaltyPerSecond);
                score = Math.Max(0, score - penalty);
                detail.Add(new DetailEntry("burn", "과열 시간(초)", burn.ToString("0.0")));
                detail.Add(new DetailEntry("burnPenalty", "과열 감점", penalty.ToString()));
            }
            CraftPlan.Record(_save.run, p, new PartRecord { key = p.Part.key, score = score, detail = detail });

            if (Enum.TryParse(p.Part.producesState, out WorkpieceState next) && next != WorkpieceState.None)
            {
                if (_piece == null) SpawnPiece(next, FreshBloomTemp, 0f);
                else _piece.SetState(next);
                _save.run.workpieceState = next.ToString();
            }
            HoldPiece();
            if (_piece != null) _save.run.burnTotal = _piece.BurnTotal;
            Persist();

            _hud.Toast($"{p.Part.name} {score}점");
            FeedbackHub.Instance?.Done(_current != null ? _current.WorkPoint.position : player.transform.position);

            if (CraftPlan.IsComplete(Recipe, _save.run))
            {
                FinishRun();
                return;
            }
            RefreshOrder();
            if (_current != null) ShowStationOptions();
        }

        // ── 작업물 ──────────────────────────────────────────

        void SpawnPiece(WorkpieceState state, float temp, float burnTotal)
        {
            var go = new GameObject("Workpiece");
            _piece = go.AddComponent<Workpiece>();
            _piece.Init(metalMaterial);
            _piece.SetState(state);
            _piece.Temperature = temp;
            _piece.BurnTotal = burnTotal;
            _burnWarnedFirst = _burnWarnedLast = burnTotal > 0f;
            HoldPiece();
        }

        void HoldPiece()
        {
            if (_piece == null) return;
            PlacePiece(player.HoldPoint);
        }

        void PlacePiece(Transform at)
        {
            if (_piece == null) return;
            _piece.transform.SetParent(at, false);
            _piece.transform.localPosition = Vector3.zero;
            _piece.transform.localRotation = Quaternion.identity;
        }

        void UpdateBurn()
        {
            if (_piece == null || !_piece.IsBurning) return;
            FeedbackHub.Instance?.Burning(_piece.transform.position);
            if (!_burnWarnedFirst)
            {
                _burnWarnedFirst = true;
                _hud.Warn("쇠가 타고 있습니다! 불에서 꺼내세요");
            }
            if (!_burnWarnedLast && _piece.BurnTotal > BurnLastWarning)
            {
                _burnWarnedLast = true;
                _hud.Warn("이대로면 곧 못 쓰게 됩니다!", 3f);
            }
            if (_piece.BurnTotal > BurnRuinSeconds) Ruin();
        }

        // 너무 오래 타서 못 쓰게 됐다. 이 한 자루는 끝나고 처음부터 다시 만든다.
        void Ruin()
        {
            if (_current != null) ExitStation();
            Destroy(_piece.gameObject);
            _piece = null;
            _save.ruined++;
            ResetRun();
            Persist();
            RefreshOrder();
            _hud.Warn("작업물이 타서 못 쓰게 됐습니다. 게시판에서 다시 시작하세요", 5f);
        }

        void ResetRun()
        {
            _save.hasRun = false;
            _save.run = new CraftRun();
            _inProgress.Clear();
            _activity = null;
        }

        static string HeatWord(float t) =>
            t > Workpiece.BurnTemp ? "하얗게 타는 중!" :
            t > 1150f ? "노랑" :
            t > 1000f ? "주황" :
            t > Workpiece.WorkableTemp ? "체리빛" :
            t > 550f ? "어두운 붉은빛" : "검게 식음";

        // ── 작업 지시서 · 진열대 ─────────────────────────────

        void StartRun(RecipeDef recipe)
        {
            if (_piece != null) Destroy(_piece.gameObject);
            _piece = null;
            ResetRun();
            _save.run = new CraftRun
            {
                recipeKey = recipe.key,
                seed = Environment.TickCount ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            _save.hasRun = true;
            Persist();
            CloseOverlay();
            RefreshOrder();
            _hud.Toast($"{recipe.displayName} 제작 시작 — 지시서에서 할 수 있는 일을 확인하세요");
        }

        void FinishRun()
        {
            var recipe = Recipe;
            var run = _save.run;
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
            _save.collection.Add(record);
            if (_piece != null) Destroy(_piece.gameObject);
            _piece = null;
            ResetRun();
            Persist();
            ExitStation();
            BuildRack();
            RefreshOrder();
            ShowResult(recipe, record, true, CloseOverlay);
        }

        void ShowBoard()
        {
            var box = Ui.Box("overlay-card");
            box.Add(Ui.Label("작업 지시서 게시판", "h1"));
            var recipe = Recipe;
            if (recipe != null)
            {
                box.Add(Ui.Label($"만드는 중: {recipe.displayName} · {CraftPlan.DoneCount(recipe, _save.run)}/{CraftPlan.PartCount(recipe)} 단계", "amber"));
                box.Add(Ui.Button("만들던 것 버리고 새로 고르기", () =>
                {
                    if (_piece != null) Destroy(_piece.gameObject);
                    _piece = null;
                    ResetRun();
                    Persist();
                    ShowBoard();
                    RefreshOrder();
                }));
            }
            else
            {
                box.Add(Ui.Label("만들 물건을 고르세요.", "muted"));
                foreach (var r in recipes.Where(r => r != null))
                {
                    var card = Ui.Box("panel", "card");
                    card.Add(Ui.Label(r.displayName, "h2"));
                    card.Add(Ui.Label(r.description, "muted"));
                    var pick = r;
                    card.Add(Ui.Button("만들기", () => StartRun(pick), "btn-primary"));
                    box.Add(card);
                }
            }
            if (_save.ruined > 0) box.Add(Ui.Label($"타서 버린 작업물: {_save.ruined}개", "small", "muted"));
            box.Add(Ui.Button("닫기 (Esc)", CloseOverlay));
            _hud.ShowOverlay(box);
        }

        void ShowRack()
        {
            var box = Ui.Box("overlay-card");
            box.Add(Ui.Label($"진열대 ({_save.collection.Count})", "h1"));
            if (_save.collection.Count == 0) box.Add(Ui.Label("아직 완성한 물건이 없습니다.", "muted"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("overlay-scroll");
            foreach (var rec in Enumerable.Reverse(_save.collection))
            {
                var row = Ui.Row("panel", "record");
                row.Add(Ui.Label(Scoring.TierByKey(rec.tierKey).Name ?? rec.tierKey, "record__tier", "amber"));
                row.Add(Ui.Label($"{rec.total}점", "record__total"));
                row.Add(Ui.Label($"{rec.recipeName} · {rec.finishedAt}", "muted", "grow"));
                var r = rec;
                row.Add(Ui.Button("보기", () => ShowResult(recipes.Find(x => x != null && x.key == r.recipeKey), r, false, ShowRack)));
                scroll.Add(row);
            }
            box.Add(scroll);
            box.Add(Ui.Button("닫기 (Esc)", CloseOverlay));
            _hud.ShowOverlay(box);
        }

        void ShowResult(RecipeDef recipe, CraftRecord record, bool fresh, Action onClose)
        {
            var screen = new ResultScreen(recipe, record, fresh, onClose, fresh ? "닫기" : "목록으로");
            screen.Root.AddToClassList("overlay-card");
            _hud.ShowOverlay(screen.Root);
        }

        void CloseOverlay()
        {
            _hud.HideOverlay();
            if (_current != null && _current.Role != StationRole.Work) ExitStation();
        }

        void RefreshOrder()
        {
            var recipe = Recipe;
            if (recipe == null)
            {
                _hud.SetWorkOrder("만들 물건 없음", "게시판에서 고르세요", Array.Empty<(string, bool)>());
                return;
            }
            var lines = new List<(string, bool)>();
            foreach (var p in CraftPlan.Available(recipe, _save.run))
            {
                bool doing = _inProgress.ContainsKey(p.Id);
                lines.Add(($"• {p.Part.name} — {Station.KindName(p.Part.station)}{(doing ? " (하던 중)" : "")}", doing));
            }
            string piece = _piece != null ? PieceName(_piece.State) : "없음";
            _hud.SetWorkOrder(recipe.displayName,
                $"{CraftPlan.DoneCount(recipe, _save.run)}/{CraftPlan.PartCount(recipe)} 단계 · 작업물: {piece}", lines);
        }

        static string PieceName(WorkpieceState s) => s switch
        {
            WorkpieceState.Bloom => "블룸",
            WorkpieceState.Bar => "철 바",
            WorkpieceState.Blade => "칼날",
            WorkpieceState.Hardened => "담금질한 칼날",
            WorkpieceState.Ground => "간 칼날",
            WorkpieceState.Dagger => "단검",
            _ => "없음",
        };

        void UpdatePrompt()
        {
            if (_current != null || _hud.OverlayOpen || player.Nearest == null)
            {
                _hud.SetPrompt(null);
                return;
            }
            var s = player.Nearest;
            string what = s.Role switch
            {
                StationRole.Board => "만들 물건 고르기",
                StationRole.Rack => "완성품 보기",
                _ => StationTodo(s),
            };
            _hud.SetPrompt($"E — {s.DisplayName}: {what}");
        }

        string StationTodo(Station s)
        {
            var recipe = Recipe;
            if (recipe == null) return "할 일 없음";
            var names = CraftPlan.Available(recipe, _save.run).Where(p => p.Part.station == s.Kind).Select(p => p.Part.name).ToList();
            if (s is ForgeStation && _piece != null) names.Add("쇠 달구기");
            return names.Count == 0 ? "지금 할 일 없음" : string.Join(" · ", names);
        }

        // 완성품을 진열대 걸이에 하나씩 건다(기본 도형 단검, 등급마다 칼날 빛이 다르다)
        void BuildRack()
        {
            if (rack == null) return;
            foreach (Transform peg in rack)
                for (int i = peg.childCount - 1; i >= 0; i--) Destroy(peg.GetChild(i).gameObject);
            int n = Mathf.Min(rack.childCount, _save.collection.Count);
            for (int i = 0; i < n; i++)
            {
                var rec = _save.collection[_save.collection.Count - 1 - i];
                var go = new GameObject("Displayed");
                go.transform.SetParent(rack.GetChild(i), false);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                var piece = go.AddComponent<Workpiece>();
                piece.Init(metalMaterial);
                piece.SetState(WorkpieceState.Dagger);
                piece.enabled = false;
                var glow = rec.tierKey switch
                {
                    "legend" => new Color(1f, 0.75f, 0.2f) * 2.5f,
                    "famous" => new Color(0.4f, 0.6f, 1f) * 1.2f,
                    _ => Color.black,
                };
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    if (r.sharedMaterial != null && r.sharedMaterial.IsKeywordEnabled("_EMISSION")) r.sharedMaterial.SetColor("_EmissionColor", glow);
            }
        }
    }
}
