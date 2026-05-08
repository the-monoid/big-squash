using Mirror;
using Steading.Combat;
using Steading.World;
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
    [RequireComponent(typeof(EnemyActor))]
    public class EnemyController : NetworkBehaviour
    {
        [Header("Combat")]
        [SerializeField] protected int meleeDamage = 10;
        [SerializeField] protected float attackRange = 1.8f;
        [SerializeField] protected float attackCooldown = 1.2f;
        [SerializeField] protected float attackWindup = 0.28f;
        [SerializeField] protected float attackRadius = 0.85f;

        [Header("Aggro")]
        [SerializeField] protected float aggroRange = 11.5f;
        [SerializeField] protected float disengageRange = 18f;
        [SerializeField] protected float leashRange = 34f;
        [SerializeField] protected float alertMemorySeconds = 7f;
        [SerializeField] protected float retargetInterval = 0.5f;

        [Header("Reactions")]
        [SerializeField] protected float knockbackDistanceMultiplier = 0.24f;
        [SerializeField] protected float knockbackDuration = 0.18f;
        [SerializeField] protected float knockbackMaxDistance = 3.4f;

        [Header("Grounding")]
        [SerializeField] protected float groundProbeHeight = 2.9f;
        [SerializeField] protected float groundProbeDepth = 6.0f;
        [SerializeField] protected float groundSnapSpeed = 26f;
        [SerializeField] protected float maxUpwardGroundSnap = 2.2f;
        [SerializeField] protected LayerMask groundLayers = ~0;

        protected NavMeshAgent _agent;
        protected Health _health;
        protected Transform _target;
        protected Vector3 _homePosition;
        protected float _nextAttackTime;
        protected float _nextRetargetTime;
        protected float _staggerUntil;
        protected float _alertUntil;
        protected bool _attackPending;
        protected Coroutine _knockbackRoutine;
        private EnemyVisualAnimator _visual;

        protected virtual void Awake()
        {
            if (GetComponent<EnemyActor>() == null) gameObject.AddComponent<EnemyActor>();
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _visual = GetComponent<EnemyVisualAnimator>();
            if (_visual == null) _visual = gameObject.AddComponent<EnemyVisualAnimator>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (_visual == null) _visual = GetComponent<EnemyVisualAnimator>();
            if (_visual != null) _visual.EnsureRig();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _homePosition = transform.position;
            _health.Died += OnDiedServer;
            _health.Damaged += OnDamagedServer;
            KeepOnWalkableSurface(instant: true);
            _homePosition = transform.position;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_health != null) _health.Died -= OnDiedServer;
            if (_health != null) _health.Damaged -= OnDamagedServer;
        }

        [ServerCallback]
        protected virtual void Update()
        {
            if (_health.IsDead) return;
            if (Time.time < _staggerUntil)
            {
                if (_agent.hasPath) _agent.ResetPath();
                return;
            }

            if (Time.time >= _nextRetargetTime || _target == null)
            {
                _nextRetargetTime = Time.time + retargetInterval;
                _target = FindClosestPlayer();
            }

            if (_target == null)
            {
                ReturnHomeIfNeeded();
                return;
            }

            var flatTargetPos = new Vector3(_target.position.x, transform.position.y, _target.position.z);
            var dist = Vector3.Distance(flatTargetPos, transform.position);

            if (dist > leashRange || (dist > disengageRange && Time.time > _alertUntil))
            {
                _target = null;
                if (_agent.hasPath) _agent.ResetPath();
                ReturnHomeIfNeeded();
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
                if (!_attackPending && Time.time >= _nextAttackTime)
                {
                    _nextAttackTime = Time.time + attackCooldown;
                    PerformMelee(_target);
                }
            }
        }

        [ServerCallback]
        protected virtual void LateUpdate()
        {
            if (_health == null || _health.IsDead) return;
            KeepOnWalkableSurface(instant: false);
        }

        [Server]
        protected virtual void PerformMelee(Transform target)
        {
            if (target == null) return;
            StartCoroutine(ServerMeleeAfterWindup(target, Random.Range(0, 3)));
        }

        [Server]
        private System.Collections.IEnumerator ServerMeleeAfterWindup(Transform target, int variant)
        {
            _attackPending = true;
            RpcPlayAttack(variant);

            if (_agent.hasPath) _agent.ResetPath();
            yield return new WaitForSeconds(attackWindup);

            _attackPending = false;
            if (_health.IsDead || Time.time < _staggerUntil || target == null) yield break;

            var flatTargetPos = new Vector3(target.position.x, transform.position.y, target.position.z);
            var toTarget = flatTargetPos - transform.position;
            if (toTarget.sqrMagnitude > (attackRange + 0.25f) * (attackRange + 0.25f)) yield break;
            if (Vector3.Dot(transform.forward, toTarget.normalized) < 0.10f) yield break;

            var hits = Physics.OverlapSphere(transform.position + transform.forward * (attackRange * 0.55f) + Vector3.up, attackRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var th = hit.GetComponentInParent<Health>();
                if (th == null || th.gameObject == gameObject) continue;
                var playerAttack = th.GetComponent<PlayerAttack>();
                if (playerAttack == null) continue;

                th.TakeDamage(new DamageInfo
                {
                    amount = meleeDamage,
                    hitPoint = th.transform.position + Vector3.up,
                    hitDirection = transform.forward,
                    sourceNetId = netId,
                    canBeBlocked = true,
                });
                yield break;
            }
        }

        [Server]
        public void StaggerServer(float seconds)
        {
            _staggerUntil = Mathf.Max(_staggerUntil, Time.time + seconds);
            RpcPlayStagger(seconds);
        }

        // Heavy CC: enemy is knocked prone for `seconds`. Stops navigation,
        // tilts the body 75° around X to read as "knocked down". Used by
        // PlayerAttack's Shield Rush — represents the New-World shield charge
        // takedown.
        [Server]
        public void KnockdownServer(float seconds)
        {
            if (_health.IsDead) return;
            _staggerUntil = Mathf.Max(_staggerUntil, Time.time + seconds);
            _attackPending = false;
            if (_agent != null && _agent.hasPath) _agent.ResetPath();
            RpcPlayKnockdown(seconds);
        }

        [ClientRpc]
        private void RpcPlayKnockdown(float seconds)
        {
            if (_visual == null) _visual = GetComponent<EnemyVisualAnimator>();
            if (_visual != null) _visual.PlayStagger(seconds);   // reuse stagger pose for now; swap for prone clip when authored
            StartCoroutine(KnockdownTilt(seconds));
        }

        private System.Collections.IEnumerator KnockdownTilt(float seconds)
        {
            var startRot = transform.rotation;
            var proneRot = startRot * Quaternion.Euler(75f, 0f, 0f);
            var lerpDur = 0.20f;
            var t = 0f;
            while (t < lerpDur) { t += Time.deltaTime; transform.rotation = Quaternion.Slerp(startRot, proneRot, Mathf.Clamp01(t / lerpDur)); yield return null; }
            transform.rotation = proneRot;
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - lerpDur * 2f));
            t = 0f;
            while (t < lerpDur) { t += Time.deltaTime; transform.rotation = Quaternion.Slerp(proneRot, startRot, Mathf.Clamp01(t / lerpDur)); yield return null; }
            transform.rotation = startRot;
        }

        [Server]
        public void KnockbackServer(Vector3 direction, float impulse)
        {
            if (_health.IsDead) return;
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.001f) flat = -transform.forward;
            flat.Normalize();

            if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine);
            var distance = Mathf.Clamp(impulse * knockbackDistanceMultiplier, 1.1f, knockbackMaxDistance);
            _knockbackRoutine = StartCoroutine(ServerKnockback(flat, distance));
        }

        [Server]
        public void ApplyWaveDifficulty(int waveIndex, float damagePerWave)
        {
            if (waveIndex <= 0) return;

            meleeDamage += Mathf.RoundToInt(waveIndex * damagePerWave);
            attackCooldown = Mathf.Max(0.72f, attackCooldown - waveIndex * 0.035f);
            if (_agent != null)
            {
                _agent.speed += Mathf.Min(0.75f, waveIndex * 0.08f);
                _agent.acceleration += Mathf.Min(3f, waveIndex * 0.35f);
            }
        }

        [ClientRpc]
        private void RpcPlayAttack(int variant)
        {
            if (_visual == null) _visual = GetComponent<EnemyVisualAnimator>();
            if (_visual != null) _visual.PlayAttack(variant);
        }

        [ClientRpc]
        private void RpcPlayStagger(float seconds)
        {
            if (_visual == null) _visual = GetComponent<EnemyVisualAnimator>();
            if (_visual != null) _visual.PlayStagger(seconds);
        }

        [Server]
        protected virtual Transform FindClosestPlayer()
        {
            Transform closest = null;
            float bestSqr = float.MaxValue;
            var aggroSqr = aggroRange * aggroRange;
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn?.identity == null) continue;
                var ph = conn.identity.GetComponent<Health>();
                if (ph == null || ph.IsDead) continue;
                var sqr = (conn.identity.transform.position - transform.position).sqrMagnitude;
                if (sqr > aggroSqr) continue;
                if (sqr < bestSqr) { bestSqr = sqr; closest = conn.identity.transform; }
            }
            return closest;
        }

        [Server]
        protected virtual void OnDamagedServer(DamageInfo info)
        {
            _alertUntil = Time.time + alertMemorySeconds;
            if (info.sourceNetId == 0) return;
            if (!NetworkServer.spawned.TryGetValue(info.sourceNetId, out var source)) return;
            _target = source.transform;
            _nextRetargetTime = Time.time + retargetInterval;
        }

        [Server]
        private System.Collections.IEnumerator ServerKnockback(Vector3 direction, float distance)
        {
            _attackPending = false;
            _staggerUntil = Mathf.Max(_staggerUntil, Time.time + knockbackDuration + 0.12f);

            var wasStopped = _agent != null && _agent.isStopped;
            if (_agent != null && _agent.hasPath) _agent.ResetPath();
            if (_agent != null) _agent.isStopped = true;

            var start = transform.position;
            var end = start + direction * distance;
            if (NavMesh.SamplePosition(end, out var hit, 1.75f, NavMesh.AllAreas))
            {
                end = hit.position;
            }

            var elapsed = 0f;
            while (elapsed < knockbackDuration && !_health.IsDead)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, knockbackDuration));
                t = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.Warp(transform.position);
                _agent.isStopped = wasStopped;
            }
            _knockbackRoutine = null;
        }

        [Server]
        private void KeepOnWalkableSurface(bool instant)
        {
            if (!TryFindWalkableGround(out var hit)) return;

            var targetY = hit.point.y;
            var surface = hit.collider.GetComponentInParent<WalkableSurface>();
            if (surface != null) targetY += surface.SnapOffset;

            var current = transform.position;
            var deltaY = targetY - current.y;
            if (Mathf.Abs(deltaY) <= 0.025f) return;
            if (deltaY > maxUpwardGroundSnap) return;

            var nextY = instant
                ? targetY
                : Mathf.MoveTowards(current.y, targetY, groundSnapSpeed * Time.deltaTime);

            transform.position = new Vector3(current.x, nextY, current.z);
        }

        [Server]
        private bool TryFindWalkableGround(out RaycastHit bestHit)
        {
            bestHit = default(RaycastHit);
            var origin = transform.position + Vector3.up * groundProbeHeight;
            var hits = Physics.RaycastAll(origin, Vector3.down, groundProbeHeight + groundProbeDepth, groundLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<EnemyActor>() != null) continue;

                var surface = hit.collider.GetComponentInParent<WalkableSurface>();
                if (surface != null)
                {
                    bestHit = hit;
                    return true;
                }

                if (IsNamedWorldGround(hit.collider.transform))
                {
                    bestHit = hit;
                    return true;
                }
            }

            return false;
        }

        private static bool IsNamedWorldGround(Transform target)
        {
            while (target != null)
            {
                if (target.name == "Ground" ||
                    target.name == "ProceduralMeadows" ||
                    target.name == "HearthStonePad")
                {
                    return true;
                }
                target = target.parent;
            }
            return false;
        }

        [Server]
        private void ReturnHomeIfNeeded()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            var flatHome = new Vector3(_homePosition.x, transform.position.y, _homePosition.z);
            if ((flatHome - transform.position).sqrMagnitude <= 2.25f)
            {
                if (_agent.hasPath) _agent.ResetPath();
                return;
            }
            _agent.SetDestination(_homePosition);
        }

        [Server]
        protected virtual void OnDiedServer()
        {
            // Future: ragdoll, loot drop, XP. M2 phase 1: just despawn.
            NetworkServer.Destroy(gameObject);
        }
    }
}
