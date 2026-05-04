using Mirror;
using Steading.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.AI
{
    // Minimal server-authoritative chaser. Picks the closest connected player,
    // navigates to attack range, melees on cooldown. The proper behavior tree
    // lands in M2 phase 2 — this exists to validate the Mirror + NavMesh +
    // damage flow end-to-end.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyController : NetworkBehaviour
    {
        [Header("Combat")]
        [SerializeField] protected int meleeDamage = 10;
        [SerializeField] protected float attackRange = 1.8f;
        [SerializeField] protected float attackCooldown = 1.2f;

        [Header("Aggro")]
        [SerializeField] protected float aggroRange = 30f;
        [SerializeField] protected float retargetInterval = 0.5f;

        protected NavMeshAgent _agent;
        protected Health _health;
        protected Transform _target;
        protected float _nextAttackTime;
        protected float _nextRetargetTime;

        protected virtual void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Died += OnDiedServer;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_health != null) _health.Died -= OnDiedServer;
        }

        [ServerCallback]
        protected virtual void Update()
        {
            if (_health.IsDead) return;

            if (Time.time >= _nextRetargetTime || _target == null)
            {
                _nextRetargetTime = Time.time + retargetInterval;
                _target = FindClosestPlayer();
            }

            if (_target == null) return;

            var flatTargetPos = new Vector3(_target.position.x, transform.position.y, _target.position.z);
            var dist = Vector3.Distance(flatTargetPos, transform.position);

            if (dist > aggroRange)
            {
                if (_agent.hasPath) _agent.ResetPath();
                return;
            }

            if (dist > attackRange)
            {
                if (_agent.isOnNavMesh) _agent.SetDestination(_target.position);
            }
            else
            {
                if (_agent.hasPath) _agent.ResetPath();
                transform.LookAt(flatTargetPos);
                if (Time.time >= _nextAttackTime)
                {
                    _nextAttackTime = Time.time + attackCooldown;
                    PerformMelee(_target);
                }
            }
        }

        [Server]
        protected virtual void PerformMelee(Transform target)
        {
            var th = target.GetComponent<Health>();
            if (th == null) return;
            th.TakeDamage(new DamageInfo
            {
                amount = meleeDamage,
                hitPoint = target.position,
                hitDirection = transform.forward,
                sourceNetId = netId,
            });
        }

        [Server]
        protected virtual Transform FindClosestPlayer()
        {
            Transform closest = null;
            float bestSqr = float.MaxValue;
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn?.identity == null) continue;
                var ph = conn.identity.GetComponent<Health>();
                if (ph == null || ph.IsDead) continue;
                var sqr = (conn.identity.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; closest = conn.identity.transform; }
            }
            return closest;
        }

        [Server]
        protected virtual void OnDiedServer()
        {
            // Future: ragdoll, loot drop, XP. M2 phase 1: just despawn.
            NetworkServer.Destroy(gameObject);
        }
    }
}
