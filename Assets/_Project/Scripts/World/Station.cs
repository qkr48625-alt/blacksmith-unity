using Blacksmith.Crafting;
using Unity.Cinemachine;
using UnityEngine;

namespace Blacksmith.World
{
    // 작업대에서 하는 일 말고, 대장간 안의 특별한 자리(지시서 게시판 · 진열대).
    public enum StationRole
    {
        Work,
        Board,
        Rack,
        Customer,   // 의뢰를 들고 찾아온 손님(실행 중에 생긴다)
    }

    // 작업대 공통. 대장장이가 approach 자리에 와서 붙으면 view 카메라로 넘어간다.
    public class Station : MonoBehaviour
    {
        [SerializeField] string displayName = "작업대";
        [SerializeField] StationRole role = StationRole.Work;
        [SerializeField] StationKind kind = StationKind.Bench;
        [SerializeField] Transform approach;
        [SerializeField] Transform workPoint;
        [SerializeField] CinemachineCamera view;

        public string DisplayName => displayName;
        public StationRole Role => role;
        public StationKind Kind => kind;
        public Transform Approach => approach != null ? approach : transform;
        public Transform WorkPoint => workPoint != null ? workPoint : transform;
        public bool IsActive { get; private set; }

        public void Setup(string name, StationRole r, StationKind k, Transform approachPoint, Transform work, CinemachineCamera cam)
        {
            displayName = name;
            role = r;
            kind = k;
            approach = approachPoint;
            workPoint = work;
            view = cam;
        }

        public virtual void Enter()
        {
            IsActive = true;
            if (view != null) view.Priority = 20;
        }

        public virtual void Exit()
        {
            IsActive = false;
            if (view != null) view.Priority = 0;
        }

        // 작업대 몸통이 차지하는 공간(불티 같은 파티클은 뺀다)
        public Bounds VisualBounds()
        {
            Bounds b = default;
            bool any = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) b = r.bounds;
                else b.Encapsulate(r.bounds);
                any = true;
            }
            return any ? b : new Bounds(transform.position + Vector3.up, Vector3.one);
        }

        public static string KindName(StationKind k) => k switch
        {
            StationKind.Forge => "화덕",
            StationKind.Anvil => "모루",
            StationKind.Quench => "물통",
            StationKind.Grind => "숫돌",
            _ => "작업대",
        };
    }
}
