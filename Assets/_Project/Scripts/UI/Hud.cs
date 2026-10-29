using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blacksmith.UI
{
    // 3D 대장간 위에 얹는 최소한의 화면: 작업 지시서, 상호작용 안내, 작업대 조작 안내, 고르기 버튼, 알림, 큰 창(결과 · 목록).
    // 빈 자리는 클릭이 통과해서 아래 대장간을 누를 수 있다.
    public sealed class Hud
    {
        public const float ToastSeconds = 2.6f;

        readonly VisualElement _root, _orderList, _options, _panelHost, _overlay, _overlayBody, _toasts;
        readonly Label _orderTitle, _orderProgress, _prompt, _hint, _warning;
        float _warningT;
        readonly List<(Label label, float t)> _toastItems = new();

        public Hud(VisualElement root)
        {
            _root = root;
            root.AddToClassList("hud");
            root.pickingMode = PickingMode.Ignore;

            var order = Ui.Box("panel", "hud-order");
            _orderTitle = Ui.Label("", "h2");
            _orderProgress = Ui.Label("", "small", "muted");
            _orderList = Ui.Box("hud-order__list");
            order.Add(Ui.Label("작업 지시서", "small", "amber"));
            order.Add(_orderTitle);
            order.Add(_orderProgress);
            order.Add(_orderList);
            root.Add(order);

            _toasts = Ui.Box("hud-toasts");
            _toasts.pickingMode = PickingMode.Ignore;
            root.Add(_toasts);

            _warning = Ui.Label("", "hud-warning");
            _warning.pickingMode = PickingMode.Ignore;
            root.Add(_warning);

            _panelHost = Ui.Box("hud-panel-host");
            _panelHost.pickingMode = PickingMode.Ignore;
            root.Add(_panelHost);

            var bottom = Ui.Box("hud-bottom");
            bottom.pickingMode = PickingMode.Ignore;
            _options = Ui.Box("hud-options");
            _hint = Ui.Label("", "panel", "hud-hint");
            _prompt = Ui.Label("", "panel", "hud-prompt");
            bottom.Add(_options);
            bottom.Add(_hint);
            bottom.Add(_prompt);
            root.Add(bottom);

            _overlay = Ui.Box("hud-overlay");
            _overlayBody = Ui.Box("hud-overlay__body");
            _overlay.Add(_overlayBody);
            root.Add(_overlay);

            SetPrompt(null);
            SetHint(null);
            ClearOptions();
            HideOverlay();
            _warning.style.display = DisplayStyle.None;
        }

        public bool OverlayOpen => _overlay.style.display == DisplayStyle.Flex;

        public void Tick(float dt)
        {
            if (_warningT > 0f)
            {
                _warningT -= dt;
                if (_warningT <= 0f) _warning.style.display = DisplayStyle.None;
            }
            for (int i = _toastItems.Count - 1; i >= 0; i--)
            {
                var (label, t) = _toastItems[i];
                t -= dt;
                if (t <= 0f)
                {
                    label.RemoveFromHierarchy();
                    _toastItems.RemoveAt(i);
                    continue;
                }
                label.style.opacity = Mathf.Clamp01(t / 0.4f);
                _toastItems[i] = (label, t);
            }
        }

        // 화면 좌표(Input System, 왼쪽 아래가 원점)에 클릭을 받는 UI 가 있는가
        public bool IsPointerOver(Vector2 screen)
        {
            var panel = _root.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            // 패널 맨 위 뿌리가 집히는 경우가 있어서, HUD 안의 요소가 집혔을 때만 UI 로 본다
            var picked = panel.Pick(p);
            return picked != null && IsUnder(picked, _root);
        }

        static bool IsUnder(VisualElement e, VisualElement ancestor)
        {
            for (var x = e.parent; x != null; x = x.parent)
                if (x == ancestor) return true;
            return false;
        }

        public void SetWorkOrder(string title, string progress, IEnumerable<(string text, bool highlight)> lines)
        {
            _orderTitle.text = title;
            _orderProgress.text = progress;
            _orderList.Clear();
            foreach (var (text, highlight) in lines)
                _orderList.Add(Ui.Label(text, "small", highlight ? "amber" : "hud-order__line"));
        }

        public void SetPrompt(string text) => SetText(_prompt, text);
        public void SetHint(string text) => SetText(_hint, text);

        static void SetText(Label l, string text)
        {
            l.text = text ?? "";
            l.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ClearOptions()
        {
            _options.Clear();
            _options.style.display = DisplayStyle.None;
        }

        public void AddOption(string label, Action onClick, bool enabled = true, bool primary = false)
        {
            var b = Ui.Button(label, onClick, primary ? "btn-primary" : "btn-option");
            b.SetEnabled(enabled);
            _options.Add(b);
            _options.style.display = DisplayStyle.Flex;
        }

        public void Toast(string text)
        {
            var l = Ui.Label(text, "panel", "hud-toast");
            l.pickingMode = PickingMode.Ignore;
            _toasts.Add(l);
            _toastItems.Add((l, ToastSeconds));
        }

        public void Warn(string text, float seconds = 2.5f)
        {
            _warning.text = text;
            _warning.style.display = DisplayStyle.Flex;
            _warningT = seconds;
        }

        public void ShowPanel(VisualElement panel)
        {
            if (panel.parent != _panelHost) _panelHost.Add(panel);
        }

        public void HidePanel(VisualElement panel)
        {
            if (panel.parent == _panelHost) panel.RemoveFromHierarchy();
        }

        // 화면 가운데 큰 창(결과 · 지시서 · 진열대). content 는 스스로 닫기 버튼을 갖는다.
        public void ShowOverlay(VisualElement content)
        {
            _overlayBody.Clear();
            _overlayBody.Add(content);
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void HideOverlay()
        {
            _overlayBody.Clear();
            _overlay.style.display = DisplayStyle.None;
        }
    }
}
