using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith.Activities;
using Blacksmith.Core;
using Blacksmith.Crafting;
using Blacksmith.Economy;
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
    // 작업물(온도 · 과열 · 버리기), 완성 → 진열대. 경영(하루 · 손님 · 의뢰 · 시장 · 업그레이드)은 WorkshopGame.Economy.cs.
    [RequireComponent(typeof(UIDocument))]
    public partial class WorkshopGame : MonoBehaviour
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
        QuestMarkers _markers;
        float _questRefreshT;   // 쇠 온도에 따라 퀘스트 안내가 바뀌어서 가끔 다시 그린다
        HashSet<Station> _targets = new();
        readonly Dictionary<Station, Vector3> _tagAnchors = new();

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

            _markers = new GameObject("Quest Markers").AddComponent<QuestMarkers>();
            _markers.Init(metalMaterial);

            if (_save.hasRun && Recipe == null) ResetRun();
            if (_save.hasRun && Enum.TryParse(_save.run.workpieceState, out WorkpieceState st) && st != WorkpieceState.None)
                SpawnPiece(st, Workpiece.RoomTemp, _save.run.burnTotal);

            BuildRack();
            StartEconomy();
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
            UpdateEconomy();
            UpdateDebugKeys(kb);

            // 접근성: T 로 쇠 온도 숫자 켜고 끄기
            if (kb != null && kb.tKey.wasPressedThisFrame && !_hud.OverlayOpen)
            {
                _save.showTemp = !_save.showTemp;
                Persist();
                _hud.Toast(_save.showTemp ? "쇠 온도 숫자 보기 켬 (T)" : "쇠 온도 숫자 보기 끔 (T)");
            }
            _hud.SetTemp(_save.showTemp && _piece != null ? $"쇠 {Mathf.RoundToInt(_piece.Temperature)}℃ · {HeatWord(_piece.Temperature)}" : null);

            _markers.Hidden = _current != null || _hud.OverlayOpen;
            UpdateTags();
            _questRefreshT -= dt;
            if (_questRefreshT <= 0f)
            {
                _questRefreshT = 0.5f;
                RefreshOrder();
            }
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
            player.SetBodyVisible(false);   // 작업대 시점에서 몸이 작업대를 가리지 않게
            s.Enter();
            _hud.SetPrompt(null);

            switch (s.Role)
            {
                case StationRole.Board: ShowBoard(); break;
                case StationRole.Rack: ShowRack(); break;
                case StationRole.Customer: ShowCustomer(s); break;
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
            player.SetBodyVisible(true);
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
                int mins = EconomyRules.MinutesOf(p);
                bool time = HasTimeFor(p);
                string label = !ok ? $"{p.Part.name} — 작업물을 들고 오세요"
                    : !time ? $"{p.Part.name} — 오늘은 시간이 모자랍니다({mins}분)"
                    : $"{p.Part.name} 시작 ({mins}분)";
                options.Add((label, () => StartActivity(part), ok && time));
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
                act.Begin(new ActivityContext { Station = _current, Piece = _piece, Game = new MinigameContext(seed, EconomyRules.ModsOf(_save.eco)), Hud = _hud });
                _inProgress[p.Id] = act;
            }
            if (p.Part.needsWorkpiece) PlacePiece(_current.WorkPoint);
            else if (_piece != null)
            {
                // 작업물이 필요 없는 일: 들고 있던 쇠가 가까운 카메라를 가리지 않게 작업대 옆에 내려 둔다
                _piece.transform.SetParent(_current.transform, false);
                _piece.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            }
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
            // 재료 등급 보정
            int withMat = EconomyRules.ApplyMaterial(_save.pick, p, score, out var why);
            if (withMat != score)
            {
                detail.Add(new DetailEntry("material", "재료 보정", why));
                score = withMat;
            }
            SpendTime(EconomyRules.MinutesOf(p));
            var before = CraftPlan.Available(Recipe, _save.run).Select(x => x.Id).ToHashSet();
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
            var unlocked = CraftPlan.Available(Recipe, _save.run).Where(x => !before.Contains(x.Id)).Select(x => x.Part.name).ToList();
            if (unlocked.Count > 0) _hud.Toast($"새 퀘스트: {string.Join(" · ", unlocked)}");
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
            _hud.Toast($"{recipe.displayName} 제작 시작 — 금빛 화살표가 가리키는 곳으로 가세요");
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
                day = _save.eco.day,
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

        // 게시판 · 진열대 창은 WorkshopGame.Economy.cs (의뢰 · 시장 · 업그레이드 · 판매)

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

        // 퀘스트 창: 지금 할 일(어디로 가서 무엇을) + 공정별 진행. 가야 할 작업대에는 화살표 · 빛을 띄운다.
        void RefreshOrder()
        {
            var recipe = Recipe;
            var targets = new HashSet<Station>();
            var lines = new List<(string, string)>();
            if (recipe == null)
            {
                var board = _stations.FirstOrDefault(s => s.Role == StationRole.Board);
                if (board != null) targets.Add(board);
                lines.Add(("▶ 만들 물건 고르기", "quest-now"));
                lines.Add(($"{To(board != null ? board.DisplayName : "게시판")} 가서 E 또는 클릭", "quest-sub"));
                EconomyQuest(lines, targets);
                _hud.SetWorkOrder("만들 물건 없음", "게시판에서 고르세요", lines);
                _markers.Show(targets);
                _targets = targets;
                return;
            }

            var available = CraftPlan.Available(recipe, _save.run);
            lines.Add(("지금 할 일", "quest-head"));
            foreach (var p in available)
            {
                var station = StationOf(p.Part.station);
                bool doing = _inProgress.ContainsKey(p.Id);
                lines.Add(($"▶ {p.Part.name}{(doing ? " (하던 중)" : "")}", "quest-now"));
                // 식은 쇠로 모루 일을 하면 점수가 깎이니, 화덕부터 들르게 한다
                if (NeedsHeat(p) && _current != station)
                {
                    var forge = StationOf(StationKind.Forge);
                    if (forge != null) targets.Add(forge);
                    lines.Add((_current is ForgeStation
                        ? $"달구는 중 · 쇠: {HeatWord(_piece.Temperature)} — 체리빛이 넘으면 {To(Station.KindName(p.Part.station))}"
                        : $"쇠가 식었어요 — {To(forge != null ? forge.DisplayName : "화덕")} 가서 달군 뒤 {Station.KindName(p.Part.station)}에서", "quest-sub"));
                    continue;
                }
                if (station != null) targets.Add(station);
                string where = station != null ? station.DisplayName : Station.KindName(p.Part.station);
                lines.Add((_current != null && _current == station ? "여기서 하는 중" : $"{To(where)} 가서 E 또는 클릭", "quest-sub"));
            }

            lines.Add(("진행", "quest-head"));
            foreach (var st in recipe.stages)
            {
                int total = st.process.parts.Count;
                int done = st.process.parts.Count(x => _save.run.IsDone(st.process.key, x.key));
                bool open = available.Any(a => a.Process == st.process);
                if (done == total) lines.Add(($"√ {st.process.title}", "quest-done"));
                else if (done > 0 || open) lines.Add(($"● {st.process.title}  {done}/{total}", "quest-current"));
                else lines.Add(($"· {st.process.title}", "quest-locked"));
            }

            EconomyQuest(lines, targets);
            string piece = _piece != null ? PieceName(_piece.State) : "없음";
            _hud.SetWorkOrder(recipe.displayName,
                $"{CraftPlan.DoneCount(recipe, _save.run)}/{CraftPlan.PartCount(recipe)} 단계 · 작업물: {piece}", lines);
            _markers.Show(targets);
            _targets = targets;
        }

        bool NeedsHeat(PartRef p) =>
            ActivityRegistry.NeedsHotPiece(p) && _piece != null && _piece.Temperature < Workpiece.WorkableTemp
            && !_inProgress.ContainsKey(p.Id);

        Station StationOf(StationKind kind) => _stations.FirstOrDefault(s => s.Role == StationRole.Work && s.Kind == kind);

        // "화덕으로" · "모루로" — 받침이 있으면(ㄹ 빼고) "으로"
        static string To(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            char c = name[^1];
            if (c < 0xAC00 || c > 0xD7A3) return name + "(으)로";
            int jong = (c - 0xAC00) % 28;
            return name + (jong == 0 || jong == 8 ? "로" : "으로");
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

        // 작업대마다 바닥 앞쪽에 이름표. 지금 가야 할 곳은 금색.
        void UpdateTags()
        {
            bool show = _current == null && !_hud.OverlayOpen;
            var cam = Camera.main;
            foreach (var s in _stations)
            {
                if (s == null) continue;
                // 손님은 움직이니 머리 위를 따라간다
                if (s.Role == StationRole.Customer)
                {
                    _hud.WorldLabel(s, s.DisplayName, s.transform.position + Vector3.up * 2.05f, cam, show, _targets.Contains(s));
                    continue;
                }
                if (!_tagAnchors.TryGetValue(s, out var at))
                {
                    var b = s.VisualBounds();
                    var front = new Vector3(b.center.x, 0f, b.center.z);
                    var stand = s.Approach.position;
                    at = Vector3.Lerp(new Vector3(stand.x, 0f, stand.z), front, 0.45f);
                    _tagAnchors[s] = at;
                }
                _hud.WorldLabel(s, s.DisplayName, at, cam, show, _targets.Contains(s));
            }
        }

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
