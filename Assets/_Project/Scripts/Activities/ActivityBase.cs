using Blacksmith.Crafting;
using Blacksmith.Minigames;
using Blacksmith.Pieces;
using Blacksmith.UI;
using Blacksmith.World;
using Unity.Cinemachine;
using UnityEngine;

namespace Blacksmith.Activities
{
    // 3D 활동의 공통 뼈대: 소품 뿌리 · 가까운 카메라 · 누름 판정을 갖고, 작업대를 떠나면 숨겼다가 돌아오면 이어 간다.
    // 끝나면 Finish 로 점수를 내고 소품 · 카메라를 지운다.
    public abstract class ActivityBase : IStationActivity
    {
        protected Station Station;
        protected Workpiece Piece;
        protected Hud Hud;
        protected System.Random Rng;
        protected Transform Props;          // 이 활동이 만든 소품의 뿌리
        protected CinemachineCamera Cam;
        protected Press Click, Space;
        protected bool Active;
        protected float T;                  // 작업대에 붙어 있던 시간

        protected ActivityBase(PartRef part) => Part = part;

        public PartRef Part { get; }
        public bool IsDone { get; private set; }
        public MinigameResult Result { get; private set; }
        public abstract string Hint { get; }

        protected float Rand => (float)Rng.NextDouble();
        MinigameContext _game;

        // 업그레이드 보정값(없으면 fallback)
        protected float Mod(string key, float fallback) => _game != null ? _game.Mod(key, fallback) : fallback;

        public void Begin(ActivityContext ctx)
        {
            Station = ctx.Station;
            Piece = ctx.Piece;
            Hud = ctx.Hud;
            Rng = ctx.Game.Rng;
            _game = ctx.Game;
            Props = new GameObject($"Activity {Part.Part.key}").transform;
            Props.SetParent(Station.transform, false);
            Build();
            Props.gameObject.SetActive(false);
        }

        // 소품 · 카메라를 만든다(Props 아래, Cam 에)
        protected abstract void Build();
        protected abstract void Tick();
        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }

        public void Enter()
        {
            Active = true;
            Click.Reset();
            Space.Reset();
            if (Props != null) Props.gameObject.SetActive(true);
            ActivityKit.SetView(Cam, true);
            OnEnter();
        }

        public void Exit()
        {
            Active = false;
            if (Props != null) Props.gameObject.SetActive(false);
            ActivityKit.SetView(Cam, false);
            OnExit();
        }

        public void Tick(float dt)
        {
            if (IsDone || !Active) return;
            T += dt;
            Click.Update(ActivityKit.MouseHeld);
            Space.Update(ActivityKit.SpaceHeld);
            Tick();
        }

        protected void Finish(MinigameResult result)
        {
            Result = result;
            OnExit();
            ActivityKit.SetView(Cam, false);
            if (Cam != null) Object.Destroy(Cam.gameObject);
            if (Props != null) Object.Destroy(Props.gameObject);
            IsDone = true;
        }

        protected void Toast(string text) => Hud?.Toast(text);

        // 일하는 자리에서 대장장이가 서는 쪽(수평)
        protected Vector3 ToStand
        {
            get
            {
                var d = Station.Approach.position - Station.WorkPoint.position;
                d.y = 0f;
                return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.back;
            }
        }

        // 서는 쪽을 +z 로 보는 회전(소품을 대장장이 쪽으로 돌려 놓을 때)
        protected Quaternion FacingStand => Quaternion.LookRotation(ToStand);

        // 일하는 자리를 서는 쪽에서 내려다보는 가까운 카메라
        protected void MakeView(float back, float up, Vector3 lookOffset = default, float fov = 40f)
        {
            var w = Station.WorkPoint.position;
            Cam = ActivityKit.View(Props.parent, w + ToStand * back + Vector3.up * up, w + lookOffset, fov);
        }

        // 작업물을 일하는 자리에 놓는다(활동이 직접 움직일 때)
        protected void PlacePiece(Vector3 worldPos, Quaternion worldRot)
        {
            if (Piece == null) return;
            Piece.transform.SetParent(Station.WorkPoint, true);
            Piece.transform.SetPositionAndRotation(worldPos, worldRot);
        }
    }
}
