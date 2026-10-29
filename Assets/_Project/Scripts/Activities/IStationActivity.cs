using Blacksmith.Crafting;
using Blacksmith.Minigames;
using Blacksmith.Pieces;
using Blacksmith.UI;
using Blacksmith.World;

namespace Blacksmith.Activities
{
    // 활동이 시작할 때 받는 것들
    public sealed class ActivityContext
    {
        public Station Station;
        public Workpiece Piece;      // 들고 온 작업물(없을 수 있음)
        public MinigameContext Game; // seed · 보정값
        public Hud Hud;
    }

    // 작업대에서 하는 세부 단계 하나(3D 판 미니게임). 대장장이가 작업대를 떠나면 Exit, 돌아오면 Enter 를 받고
    // 하던 데서 이어 간다. 끝나면 IsDone 과 Result(점수 0~100 + 측정값)를 낸다.
    public interface IStationActivity
    {
        PartRef Part { get; }
        // 작업대에 붙어 있는 동안 HUD 아래에 띄우는 조작 안내
        string Hint { get; }
        void Begin(ActivityContext ctx);
        void Enter();
        void Exit();
        void Tick(float dt);
        bool IsDone { get; }
        MinigameResult Result { get; }
    }
}
