using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith.Crafting;
using Blacksmith.Economy;
using Blacksmith.Feedback;
using Blacksmith.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blacksmith.World
{
    // 경영 루프: 하루(8시~20시, 세부 단계마다 대장간 시간이 든다) · 아침에 문으로 들어오는 손님과 의뢰 · 게시판(의뢰 납품 · 만들기 ·
    // 시장 · 업그레이드 · 하루 마치기) · 진열대에서 팔기. 수치는 Economy/EconomyRules.
    public partial class WorkshopGame
    {
        static readonly Vector3[] WaitSpots = { new(-2.3f, 0f, -2.9f), new(-1.0f, 0f, -3.3f), new(2.3f, 0f, -2.9f) };
        static readonly Color[] Clothes =
        {
            new(0.62f, 0.2f, 0.18f), new(0.2f, 0.45f, 0.3f), new(0.5f, 0.4f, 0.65f), new(0.75f, 0.6f, 0.25f), new(0.3f, 0.32f, 0.36f),
        };

        readonly List<Customer> _customers = new();
        string _boardTab = "orders";
        MaterialPick _draftPick;

        EconomyState Eco => _save.eco;

        // ── 하루 ──────────────────────────────────────────

        void StartEconomy()
        {
            _save.eco ??= new EconomyState();
            _save.pick ??= new MaterialPick();
            if (!Eco.offersSpawned) MorningCustomers();
            else
                // 저장된 오늘 손님은 이미 기다리는 자리에 서 있다
                for (int i = 0; i < Eco.offers.Count; i++) SpawnCustomer(Eco.offers[i], i, true);
        }

        void UpdateEconomy()
        {
            _hud.SetStatus($"{Eco.day}일째 {EconomyRules.Clock(Eco.minutes)} · {Eco.money}실링 · 평판 {Eco.reputation}");
        }

        bool HasTimeFor(PartRef p) => Eco.minutes + EconomyRules.MinutesOf(p) <= EconomyRules.DayEnd;

        void SpendTime(int minutes)
        {
            Eco.minutes = Mathf.Min(EconomyRules.DayEnd, Eco.minutes + minutes);
            if (Eco.minutes >= EconomyRules.DayEnd - 30) _hud.Toast("해가 집니다 — 게시판에서 하루를 마치세요");
        }

        void MorningCustomers()
        {
            var rng = new System.Random(Eco.day * 7919 + Eco.reputation);
            var recipe = recipes.FirstOrDefault(r => r != null);
            Eco.offers.Clear();
            for (int i = 0; i < EconomyRules.OffersPerDay(Eco) && recipe != null; i++)
            {
                var o = EconomyRules.MakeOrder(Eco, rng, recipe.key);
                Eco.offers.Add(o);
                SpawnCustomer(o, i, false);
            }
            Eco.offersSpawned = true;
            Persist();
            if (Eco.offers.Count > 0) _hud.Toast($"{Eco.day}일째 아침 — 손님 {Eco.offers.Count}명이 찾아왔습니다");
        }

        void SpawnCustomer(Order o, int slot, bool alreadyThere)
        {
            var c = Customer.Spawn(o.id, o.customer, WaitSpots[slot % WaitSpots.Length], Clothes[o.id % Clothes.Length], alreadyThere);
            c.Gone += OnCustomerGone;
            _customers.Add(c);
            RebuildStations();
        }

        void OnCustomerGone(Customer c)
        {
            _customers.Remove(c);
            _hud.RemoveWorldLabel(c.Station);
            RebuildStations();
        }

        // 작업대 목록(대장장이가 가까이서 E 로 붙는 대상)에 지금 있는 손님을 넣는다. 손님 목록이 기준이라 막 지운 손님은 빠진다.
        void RebuildStations()
        {
            _stations = FindObjectsByType<Station>().Where(s => s != null && s.Role != StationRole.Customer).Concat(_customers.Select(c => c.Station)).ToArray();
            player.Init(_stations);
        }

        void EndDay()
        {
            var lines = new List<string>();
            // 기한이 지난 의뢰는 실패: 평판 −2
            foreach (var o in Eco.orders.Where(o => o.dueDay <= Eco.day).ToList())
            {
                Eco.orders.Remove(o);
                Eco.reputation = Mathf.Max(0, Eco.reputation - 2);
                lines.Add($"기한을 넘긴 의뢰: {o.customer} — 평판 −2");
            }
            // 오늘 손님 중 답하지 않은 손님은 돌아간다
            foreach (var c in _customers.ToList())
            {
                c.Gone -= OnCustomerGone;
                _hud.RemoveWorldLabel(c.Station);
                Destroy(c.gameObject);
            }
            _customers.Clear();
            int earned = Eco.earnedToday, delivered = Eco.deliveredToday;
            Eco.day++;
            Eco.minutes = EconomyRules.DayStart;
            Eco.offers.Clear();
            Eco.offersSpawned = false;
            Eco.earnedToday = Eco.deliveredToday = 0;
            RebuildStations();
            Persist();

            var box = Ui.Box("overlay-card");
            box.Add(Ui.Label($"{Eco.day - 1}일째 저녁 정산", "h1"));
            box.Add(Ui.Label($"오늘 번 돈 {earned}실링 · 납품 {delivered}건", "amber"));
            box.Add(Ui.Label($"가진 돈 {Eco.money}실링 · 평판 {Eco.reputation}", "h2"));
            foreach (var l in lines) box.Add(Ui.Label(l, "red"));
            if (Eco.orders.Count > 0) box.Add(Ui.Label($"남은 의뢰 {Eco.orders.Count}건 — 기한을 확인하세요", "muted"));
            box.Add(Ui.Button($"{Eco.day}일째 아침으로", () =>
            {
                _hud.HideOverlay();
                MorningCustomers();
                ShowBoard();
            }, "btn-primary"));
            _hud.ShowOverlay(box);
            RefreshOrder();
        }

        // ── 손님 ──────────────────────────────────────────

        void ShowCustomer(Station s)
        {
            var c = _customers.FirstOrDefault(x => x.Station == s);
            var o = c != null ? Eco.offers.FirstOrDefault(x => x.id == c.OrderId) : null;
            if (o == null)
            {
                ExitStation();
                return;
            }
            var box = Ui.Box("overlay-card");
            box.Add(Ui.Label(o.customer, "h1"));
            box.Add(Ui.Label($"\"{o.note}\"", "muted"));
            var card = Ui.Box("panel", "card");
            card.Add(Ui.Label(OrderLine(o), "h2"));
            card.Add(Ui.Label($"보수 {o.reward}실링 (조건을 넘긴 만큼 덤) · {o.dueDay}일째 저녁까지", "amber"));
            box.Add(card);
            bool full = Eco.orders.Count >= EconomyRules.MaxActiveOrders;
            if (full) box.Add(Ui.Label($"의뢰는 {EconomyRules.MaxActiveOrders}건까지 받을 수 있습니다", "red"));
            var row = Ui.Row("gap");
            var accept = Ui.Button("받겠습니다", () => Answer(c, o, true), "btn-primary");
            accept.SetEnabled(!full);
            row.Add(accept);
            row.Add(Ui.Button("죄송하지만 어렵겠습니다", () => Answer(c, o, false)));
            box.Add(row);
            _hud.ShowOverlay(box);
        }

        static string OrderLine(Order o) => $"단검 · {EconomyRules.StatNames[o.stat]} {o.min} 이상";

        void Answer(Customer c, Order o, bool accept)
        {
            Eco.offers.Remove(o);
            if (accept)
            {
                Eco.orders.Add(o);
                _hud.Toast($"의뢰를 받았습니다 — {OrderLine(o)}, {o.dueDay}일째까지");
            }
            Persist();
            CloseOverlay();
            c.Leave();
            RefreshOrder();
        }

        // ── 게시판 ────────────────────────────────────────

        void ShowBoard()
        {
            var box = Ui.Box("overlay-card");
            box.Add(Ui.Label("작업 지시서 게시판", "h1"));
            box.Add(Ui.Label($"{Eco.day}일째 {EconomyRules.Clock(Eco.minutes)} · {Eco.money}실링 · 평판 {Eco.reputation}", "amber"));
            var tabs = Ui.Row("gap");
            foreach (var (key, name) in new[] { ("orders", "의뢰"), ("craft", "만들기"), ("market", "시장"), ("upgrade", "업그레이드") })
            {
                var k = key;
                tabs.Add(Ui.Button(name, () =>
                {
                    _boardTab = k;
                    ShowBoard();
                }, _boardTab == key ? "btn-selected" : "btn-option"));
            }
            tabs.Add(Ui.Button("하루 마치기", () =>
            {
                _hud.HideOverlay();
                if (_current != null) ExitStation();
                EndDay();
            }));
            box.Add(tabs);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("overlay-scroll");
            switch (_boardTab)
            {
                case "craft": BoardCraft(scroll); break;
                case "market": BoardMarket(scroll); break;
                case "upgrade": BoardUpgrades(scroll); break;
                default: BoardOrders(scroll); break;
            }
            box.Add(scroll);
            box.Add(Ui.Button("닫기 (Esc)", CloseOverlay));
            _hud.ShowOverlay(box);
        }

        void BoardOrders(VisualElement root)
        {
            if (Eco.orders.Count == 0) root.Add(Ui.Label("받은 의뢰가 없습니다. 아침에 찾아오는 손님에게 말을 걸어 의뢰를 받으세요.", "muted"));
            var stock = _save.collection.Where(r => r.InStock).ToList();
            foreach (var o in Eco.orders.OrderBy(x => x.dueDay))
            {
                var card = Ui.Box("panel", "card");
                int left = o.dueDay - Eco.day;
                card.Add(Ui.Label($"{o.customer} — {OrderLine(o)}", "h2"));
                card.Add(Ui.Label($"보수 {o.reward}실링 · {(left <= 0 ? "오늘 저녁까지!" : $"{left}일 남음 ({o.dueDay}일째)")}", left <= 0 ? "red" : "amber"));
                var fits = stock.Where(r => EconomyRules.StatOf(r, o.stat) >= o.min).OrderByDescending(r => EconomyRules.StatOf(r, o.stat)).ToList();
                if (fits.Count == 0) card.Add(Ui.Label(stock.Count == 0 ? "납품할 완성품이 없습니다" : "조건에 맞는 완성품이 없습니다", "muted"));
                foreach (var r in fits.Take(3))
                {
                    var rec = r;
                    var order = o;
                    card.Add(Ui.Button($"납품: {r.recipeName} {r.total}점 ({EconomyRules.StatNames[o.stat]} {EconomyRules.StatOf(r, o.stat)}) → {EconomyRules.RewardFor(o, r)}실링",
                        () => Deliver(order, rec), "btn-primary"));
                }
                root.Add(card);
            }
        }

        void Deliver(Order o, CraftRecord rec)
        {
            int pay = EconomyRules.RewardFor(o, rec);
            int over = EconomyRules.StatOf(rec, o.stat) - o.min;
            int rep = 2 + (over >= 15 ? 1 : 0);
            Eco.money += pay;
            Eco.earnedToday += pay;
            Eco.deliveredToday++;
            Eco.reputation += rep;
            Eco.orders.Remove(o);
            rec.deliveredTo = o.id;
            Persist();
            BuildRack();
            FeedbackHub.Instance?.Done(player.transform.position);
            _hud.Toast($"{o.customer}에게 납품했습니다 — {pay}실링 · 평판 +{rep}");
            RefreshOrder();
            ShowBoard();
        }

        void BoardCraft(VisualElement root)
        {
            var recipe = Recipe;
            if (recipe != null)
            {
                root.Add(Ui.Label($"만드는 중: {recipe.displayName} · {CraftPlan.DoneCount(recipe, _save.run)}/{CraftPlan.PartCount(recipe)} 단계", "amber"));
                root.Add(Ui.Label($"재료: {EconomyRules.Mat("ore", _save.pick.ore).Name} · {EconomyRules.Mat("coal", _save.pick.coal).Name} · {EconomyRules.Mat("leather", _save.pick.leather).Name} · {EconomyRules.Mat("grip", _save.pick.grip).Name}", "muted"));
                root.Add(Ui.Button("만들던 것 버리고 새로 고르기 (재료는 돌아오지 않습니다)", () =>
                {
                    if (_piece != null) Destroy(_piece.gameObject);
                    _piece = null;
                    ResetRun();
                    Persist();
                    ShowBoard();
                    RefreshOrder();
                }));
                if (_save.ruined > 0) root.Add(Ui.Label($"타서 버린 작업물: {_save.ruined}개", "small", "muted"));
                return;
            }

            // 재료 고르기: 창고에 있는 것 중에서. 기본은 가진 것 중 가장 좋은 등급
            _draftPick ??= BestPick();
            root.Add(Ui.Label("쓸 재료를 고르세요 (창고에 있는 것만)", "muted"));
            foreach (var slot in new[] { ("ore", "광석"), ("coal", "숯"), ("leather", "가죽"), ("grip", "손잡이감") })
            {
                var row = Ui.Row("shop-row");
                row.Add(Ui.Label(slot.Item2, "record__tier"));
                foreach (var m in EconomyRules.MaterialDefs.Where(m => m.Slot == slot.Item1))
                {
                    int have = EconomyRules.Count(Eco.stock, m.Slot, m.Key);
                    var def = m;
                    bool selected = PickOf(_draftPick, m.Slot) == m.Key;
                    var b = Ui.Button($"{m.Name} ×{have}", () =>
                    {
                        SetPick(_draftPick, def.Slot, def.Key);
                        ShowBoard();
                    }, selected ? "btn-selected" : "btn-option");
                    b.SetEnabled(have > 0);
                    row.Add(b);
                }
                root.Add(row);
            }
            bool ok = new[] { "ore", "coal", "leather", "grip" }.All(s => EconomyRules.Count(Eco.stock, s, PickOf(_draftPick, s)) > 0);
            if (!ok) root.Add(Ui.Label("재료가 모자랍니다 — 시장에서 사 오세요", "red"));
            foreach (var r in recipes.Where(r => r != null))
            {
                var card = Ui.Box("panel", "card");
                card.Add(Ui.Label(r.displayName, "h2"));
                card.Add(Ui.Label(r.description + " · 대장간 시간 약 10시간", "muted"));
                var pick = r;
                var b = Ui.Button("이 재료로 만들기", () => StartRunWithMaterials(pick), "btn-primary");
                b.SetEnabled(ok);
                card.Add(b);
                root.Add(card);
            }
        }

        MaterialPick BestPick()
        {
            var p = new MaterialPick();
            foreach (var slot in new[] { "ore", "coal", "leather", "grip" })
            {
                var best = EconomyRules.MaterialDefs.Where(m => m.Slot == slot && EconomyRules.Count(Eco.stock, slot, m.Key) > 0).OrderByDescending(m => m.Price).FirstOrDefault();
                if (best.Key != null) SetPick(p, slot, best.Key);
            }
            return p;
        }

        static string PickOf(MaterialPick p, string slot) => slot switch { "ore" => p.ore, "coal" => p.coal, "leather" => p.leather, _ => p.grip };

        static void SetPick(MaterialPick p, string slot, string key)
        {
            switch (slot)
            {
                case "ore": p.ore = key; break;
                case "coal": p.coal = key; break;
                case "leather": p.leather = key; break;
                default: p.grip = key; break;
            }
        }

        void StartRunWithMaterials(RecipeDef recipe)
        {
            foreach (var slot in new[] { "ore", "coal", "leather", "grip" }) EconomyRules.Add(Eco.stock, slot, PickOf(_draftPick, slot), -1);
            _save.pick = _draftPick;
            _draftPick = null;
            StartRun(recipe);
        }

        void BoardMarket(VisualElement root)
        {
            root.Add(Ui.Label("재료를 사 둡니다. 등급이 좋을수록 그 공정 점수에 보정이 붙습니다.", "muted"));
            foreach (var m in EconomyRules.MaterialDefs)
            {
                var row = Ui.Row("panel", "record");
                row.Add(Ui.Label(m.Name, "record__tier"));
                row.Add(Ui.Label($"{m.Price}실링", "record__total", "amber"));
                row.Add(Ui.Label($"{m.Effect} · 창고 {EconomyRules.Count(Eco.stock, m.Slot, m.Key)}", "muted", "grow"));
                var def = m;
                var b = Ui.Button("사기", () =>
                {
                    Eco.money -= def.Price;
                    EconomyRules.Add(Eco.stock, def.Slot, def.Key, 1);
                    Persist();
                    ShowBoard();
                });
                b.SetEnabled(Eco.money >= m.Price);
                row.Add(b);
                root.Add(row);
            }
        }

        void BoardUpgrades(VisualElement root)
        {
            root.Add(Ui.Label("도구를 바꾸면 그 공정의 손맛이 쉬워집니다. 평판이 모자라면 아직 팔지 않습니다.", "muted"));
            foreach (var u in EconomyRules.Upgrades)
            {
                var row = Ui.Row("panel", "record");
                bool owned = Eco.upgrades.Contains(u.Key);
                row.Add(Ui.Label(u.Name, "record__tier"));
                row.Add(Ui.Label(owned ? "갖춤" : $"{u.Price}실링", "record__total", "amber"));
                row.Add(Ui.Label(u.Effect + (u.Rep > 0 && !owned ? $" · 평판 {u.Rep} 필요" : ""), "muted", "grow"));
                if (!owned)
                {
                    var def = u;
                    var b = Ui.Button("사기", () =>
                    {
                        Eco.money -= def.Price;
                        Eco.upgrades.Add(def.Key);
                        Persist();
                        _hud.Toast($"{def.Name}을(를) 들였습니다 — {def.Effect}");
                        ShowBoard();
                    });
                    b.SetEnabled(Eco.money >= u.Price && Eco.reputation >= u.Rep);
                    row.Add(b);
                }
                root.Add(row);
            }
        }

        // ── 진열대 ────────────────────────────────────────

        void ShowRack()
        {
            var box = Ui.Box("overlay-card");
            var stock = _save.collection.Count(r => r.InStock);
            box.Add(Ui.Label($"진열대 (팔 수 있는 것 {stock} · 모두 {_save.collection.Count})", "h1"));
            if (_save.collection.Count == 0) box.Add(Ui.Label("아직 완성한 물건이 없습니다.", "muted"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("overlay-scroll");
            foreach (var rec in Enumerable.Reverse(_save.collection))
            {
                var row = Ui.Row("panel", "record");
                row.Add(Ui.Label(Scoring.TierByKey(rec.tierKey).Name ?? rec.tierKey, "record__tier", "amber"));
                row.Add(Ui.Label($"{rec.total}점", "record__total"));
                string state = rec.sold ? "팔림" : rec.deliveredTo != 0 ? "납품함" : "진열 중";
                row.Add(Ui.Label($"{rec.recipeName} · {state}", "muted", "grow"));
                var r = rec;
                row.Add(Ui.Button("보기", () => ShowResult(recipes.Find(x => x != null && x.key == r.recipeKey), r, false, ShowRack)));
                if (rec.InStock)
                    row.Add(Ui.Button($"팔기 {EconomyRules.SellPrice(rec)}실링", () =>
                    {
                        int price = EconomyRules.SellPrice(r);
                        r.sold = true;
                        Eco.money += price;
                        Eco.earnedToday += price;
                        Persist();
                        BuildRack();
                        _hud.Toast($"{price}실링에 팔았습니다");
                        ShowRack();
                    }));
                scroll.Add(row);
            }
            box.Add(scroll);
            box.Add(Ui.Button("닫기 (Esc)", CloseOverlay));
            _hud.ShowOverlay(box);
        }

        // ── 퀘스트 창의 경영 줄 ───────────────────────────

        void EconomyQuest(List<(string, string)> lines, HashSet<Station> targets)
        {
            var head = new List<(string, string)>();
            var waiting = _customers.Where(c => c != null && c.Arrived).ToList();
            if (waiting.Count > 0)
            {
                head.Add(($"▶ 손님 {waiting.Count}명이 기다립니다", "quest-now"));
                head.Add(("손님에게 가서 E 또는 클릭 — 의뢰 듣기", "quest-sub"));
                foreach (var c in waiting) targets.Add(c.Station);
            }
            var stock = _save.collection.Where(r => r.InStock).ToList();
            var ready = Eco.orders.Where(o => stock.Any(r => EconomyRules.StatOf(r, o.stat) >= o.min)).ToList();
            var board = _stations.FirstOrDefault(s => s != null && s.Role == StationRole.Board);
            if (ready.Count > 0)
            {
                head.Add(($"▶ 납품할 수 있는 의뢰 {ready.Count}건", "quest-now"));
                head.Add(("게시판 → 의뢰에서 납품", "quest-sub"));
                if (board != null) targets.Add(board);
            }
            if (Eco.orders.Count > 0)
            {
                head.Add(("의뢰", "quest-head"));
                foreach (var o in Eco.orders.OrderBy(x => x.dueDay))
                {
                    int left = o.dueDay - Eco.day;
                    head.Add(($"· {OrderLine(o)} — {(left <= 0 ? "오늘까지!" : $"{left}일 남음")}", left <= 0 ? "quest-now" : "quest-current"));
                }
            }
            if (Eco.minutes >= EconomyRules.DayEnd - 30)
            {
                head.Add(("▶ 해가 졌습니다 — 게시판에서 하루 마치기", "quest-now"));
                if (board != null) targets.Add(board);
            }
            if (head.Count > 0) lines.InsertRange(0, head);
        }
    }
}
