using System;
using UnityEngine.UIElements;

namespace Blacksmith.UI
{
    // 화면을 코드로 짤 때 쓰는 짧은 도우미. 모양은 Main.uss 의 클래스가 맡는다.
    public static class Ui
    {
        public static VisualElement Box(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Label(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        // 마우스로 쓰는 게임이라 버튼이 포커스를 잡지 않게 한다(포커스된 버튼은 스페이스에 눌려 버린다).
        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text, focusable = false };
            b.AddToClassList("btn");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        // 누르고 있기용 버튼. Button 은 PointerDown 을 먼저 가져가서 VisualElement 로 만든다(HoldInput 과 함께 쓴다).
        public static VisualElement HoldButton(string text, out Label label)
        {
            var b = Box("btn", "btn-hold");
            label = Label(text, "btn-hold__label");
            label.pickingMode = PickingMode.Ignore;
            b.Add(label);
            return b;
        }

        public static VisualElement Row(params string[] classes)
        {
            var e = Box(classes);
            e.AddToClassList("row");
            return e;
        }

        public static VisualElement Bar(out VisualElement fill, params string[] classes)
        {
            var track = Box("bar-track");
            foreach (var c in classes) track.AddToClassList(c);
            fill = Box("bar-fill");
            track.Add(fill);
            return track;
        }

        public static void SetPercentWidth(VisualElement e, float ratio) =>
            e.style.width = Length.Percent(Math.Clamp(ratio, 0f, 1f) * 100f);
    }
}
