using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Blacksmith.Minigames
{
    // "누르고 있기" 입력: 화면 버튼을 누르고 있거나 스페이스바를 누르고 있으면 Held.
    // Pressed / Released 는 그 프레임에 바뀌었을 때만 true 다. Poll 을 프레임마다 한 번 부른다.
    public sealed class HoldInput
    {
        readonly VisualElement _button;
        bool _pointer;
        bool _was;

        public bool Held { get; private set; }
        public bool Pressed { get; private set; }
        public bool Released { get; private set; }
        public bool Enabled { get; set; } = true;

        // button 은 Button 이 아닌 VisualElement 여야 한다(Button 은 PointerDown 을 먼저 가져간다).
        public HoldInput(VisualElement button)
        {
            _button = button;
            button.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!Enabled) return;
                _pointer = true;
                button.CapturePointer(e.pointerId);
            });
            button.RegisterCallback<PointerUpEvent>(e =>
            {
                _pointer = false;
                button.ReleasePointer(e.pointerId);
            });
            button.RegisterCallback<PointerCaptureOutEvent>(_ => _pointer = false);
        }

        public void Poll()
        {
            bool space = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            Held = Enabled && (_pointer || space);
            Pressed = Held && !_was;
            Released = !Held && _was;
            _was = Held;
            _button.EnableInClassList("btn-hold--active", Held);
        }

        // 단계가 끝나 입력을 놓을 때. 다음 Poll 에서 Released 가 다시 뜨지 않게 한다.
        public void Reset()
        {
            _pointer = false;
            _was = false;
            Held = Pressed = Released = false;
        }
    }
}
