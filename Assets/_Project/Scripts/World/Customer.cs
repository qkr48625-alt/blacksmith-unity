using System;
using Blacksmith.Crafting;
using UnityEngine;
using UnityEngine.AI;

namespace Blacksmith.World
{
    // 의뢰를 들고 문으로 들어오는 손님. 기다리는 자리까지 걸어와 서 있고, 말을 걸면 의뢰 카드가 뜬다.
    // 수락 · 거절하면 문으로 걸어 나가 사라진다. 실행 중에 만들어지고, 대장장이가 붙을 수 있게 Station(Customer) 을 단다.
    [RequireComponent(typeof(NavMeshAgent))]
    public class Customer : MonoBehaviour
    {
        public static readonly Vector3 Door = new(0f, 0f, -3.9f);   // 앞벽 문 틈 바로 안쪽(길 위)

        NavMeshAgent _agent;
        bool _leaving;

        public int OrderId { get; private set; }
        public Station Station { get; private set; }
        public bool Arrived { get; private set; }
        public event Action<Customer> Gone;

        public static Customer Spawn(int orderId, string name, Vector3 spot, Color clothes, bool alreadyThere)
        {
            var go = new GameObject($"손님 {name}");
            go.transform.position = alreadyThere ? spot : Door;
            var c = go.AddComponent<Customer>();
            c.Build(orderId, name, clothes);
            c._agent.Warp(go.transform.position);
            if (alreadyThere) c.Arrive();
            else c._agent.SetDestination(spot);
            return c;
        }

        void Build(int orderId, string name, Color clothes)
        {
            OrderId = orderId;
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = 1.6f;
            _agent.radius = 0.28f;
            _agent.height = 1.7f;
            _agent.stoppingDistance = 0.05f;

            var body = Part(PrimitiveType.Capsule, new Vector3(0f, 0.78f, 0f), new Vector3(0.5f, 0.78f, 0.42f), clothes);
            // 클릭으로 말을 걸 수 있게 몸통에만 충돌체를 둔다
            body.AddComponent<CapsuleCollider>();
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.66f, 0f), Vector3.one * 0.3f, new Color(0.85f, 0.68f, 0.55f));
            Part(PrimitiveType.Cylinder, new Vector3(0f, 1.84f, 0f), new Vector3(0.34f, 0.04f, 0.34f), clothes * 0.6f);   // 모자 챙
            Part(PrimitiveType.Cylinder, new Vector3(0f, 1.9f, 0f), new Vector3(0.2f, 0.07f, 0.2f), clothes * 0.6f);

            var approach = new GameObject("Approach").transform;
            approach.SetParent(transform, false);
            approach.localPosition = new Vector3(0f, 0f, 0.9f);
            Station = gameObject.AddComponent<Station>();
            Station.Setup(name, StationRole.Customer, StationKind.Bench, approach, transform, null);
        }

        GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.material.color = color;
            return g;
        }

        void Arrive()
        {
            Arrived = true;
            // 대장간 안쪽(작업대들)을 바라본다
            transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        public void Leave()
        {
            _leaving = true;
            Arrived = false;
            _agent.SetDestination(Door);
        }

        void Update()
        {
            if (_agent.pathPending || _agent.remainingDistance > 0.1f) return;
            if (_leaving)
            {
                Gone?.Invoke(this);
                Destroy(gameObject);
            }
            else if (!Arrived) Arrive();
        }
    }
}
