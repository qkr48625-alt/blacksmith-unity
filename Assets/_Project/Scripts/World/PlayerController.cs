using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Blacksmith.World
{
    // 대장장이. WASD 로 걷고(카메라 기준 방향), 바닥을 클릭하면 그리로 걸어간다.
    // 작업대를 클릭하면 그 앞까지 걸어가서 붙고, 작업대 가까이에서 E 를 눌러도 붙는다.
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerController : MonoBehaviour
    {
        const float InteractRange = 1.3f;

        [SerializeField] Transform holdPoint;
        [SerializeField] float walkSpeed = 3.2f;

        NavMeshAgent _agent;
        Camera _cam;
        Station[] _stations;
        Station _pendingStation;   // 클릭해서 걸어가는 중인 작업대

        public Transform HoldPoint => holdPoint;
        public bool Locked { get; set; }
        public Station Nearest { get; private set; }

        public event Action<Station> InteractRequested;

        public void Bind(Transform hold) => holdPoint = hold;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = walkSpeed;
            _agent.angularSpeed = 720f;
            _agent.acceleration = 30f;
            _agent.stoppingDistance = 0.05f;
        }

        public void Init(Station[] stations)
        {
            _stations = stations;
            _cam = Camera.main;
        }

        public void FaceTowards(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
        }

        public void Stop()
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _pendingStation = null;
        }

        void Update()
        {
            if (!_agent.isOnNavMesh || _stations == null) return;
            Nearest = FindNearest();
            if (Locked) return;

            var kb = Keyboard.current;
            var move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
            }

            if (move.sqrMagnitude > 0f)
            {
                // 키보드로 움직이면 클릭 이동은 취소한다
                Stop();
                var fwd = _cam != null ? Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var dir = (fwd * move.y + right * move.x).normalized;
                _agent.Move(dir * walkSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 15f * Time.deltaTime);
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !WorkshopGame.PointerOverUi) ClickMove(mouse.position.ReadValue());

            if (kb != null && kb.eKey.wasPressedThisFrame && Nearest != null)
            {
                Stop();
                InteractRequested?.Invoke(Nearest);
            }

            // 클릭한 작업대에 다 왔으면 붙는다
            if (_pendingStation != null && !_agent.pathPending && _agent.remainingDistance <= 0.15f)
            {
                var s = _pendingStation;
                _pendingStation = null;
                InteractRequested?.Invoke(s);
            }
        }

        void ClickMove(Vector2 screen)
        {
            if (_cam == null) return;
            var ray = _cam.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 100f)) return;
            var station = hit.collider.GetComponentInParent<Station>();
            if (station != null)
            {
                _pendingStation = station;
                _agent.SetDestination(station.Approach.position);
                return;
            }
            _pendingStation = null;
            if (NavMesh.SamplePosition(hit.point, out var nav, 1f, NavMesh.AllAreas)) _agent.SetDestination(nav.position);
        }

        Station FindNearest()
        {
            Station best = null;
            float bestD = InteractRange;
            foreach (var s in _stations)
            {
                var d = Vector3.Distance(Flat(transform.position), Flat(s.Approach.position));
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
