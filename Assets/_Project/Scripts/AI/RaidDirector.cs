using System.Collections.Generic;
using Mirror;
using Steading.Building;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.AI
{
    public enum RaidKind
    {
        None = 0,
        HuntPlayer = 1,
        HuntStation = 2,
    }

    // Server-authoritative scheduler for base sieges. Every secondsBetweenRaids
    // a raid kicks off:
    //   - HuntPlayer: 4-8 Draugr spawn at the map edge nearest a random
    //     connected player. Their _overrideTarget is set to that player.
    //   - HuntStation: pick a random CraftingStation; spawn 4-8 Draugr at
    //     the map edge closest to it. _overrideTarget = station transform.
    //     Raid ends when the station is destroyed OR all Draugr are dead.
    // Falls back to HuntPlayer if no stations exist.
    //
    // Singleton — exactly one in the scene. M2Setup adds it to World_Test
    // (alongside the Draugr spawner / NavMeshSurface). The Singleton lives
    // for the lifetime of that scene; no DontDestroyOnLoad needed.
    public class RaidDirector : NetworkBehaviour
    {
        public static RaidDirector Instance { get; private set; }

        [Header("Schedule")]
        [SerializeField] private float secondsBetweenRaids = 600f;     // 10 real minutes between raids
        [SerializeField] private float firstRaidDelay = 240f;          // first raid 4 minutes in
        [SerializeField] private float raidEndTimeout = 240f;          // safety: end stale raids after 4 min

        [Header("Spawn")]
        [SerializeField] private GameObject draugrPrefab;
        [SerializeField] private int minWarbandSize = 4;
        [SerializeField] private int maxWarbandSize = 8;
        [SerializeField] private float spawnFromMapEdge = 60f;          // distance from target along the map-edge ray

        [Header("Replicated state")]
        [SyncVar] private float _nextRaidAt;
        [SyncVar] private RaidKind _activeRaid;
        [SyncVar] private string _activeTargetName;
        [SyncVar] private int _activeRemaining;
        // Set true in OnStartServer once the schedule is initialized. RaidHud
        // checks this before rendering so a freshly-connected client doesn't
        // see a "00:00" flicker before the SyncVar payload arrives.
        [SyncVar] private bool _initialized;

        public float NextRaidAt => _nextRaidAt;
        public RaidKind ActiveRaid => _activeRaid;
        public string ActiveTargetName => _activeTargetName;
        public int ActiveRemaining => _activeRemaining;
        public bool Initialized => _initialized;

        private readonly List<EnemyController> _activeMobs = new List<EnemyController>();
        private float _activeRaidStartedAt;
        private Transform _activeTarget;

        private void Awake()
        {
            // Scene-placed in World_Test by M2Setup. If a stale duplicate
            // somehow exists, win over it.
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _nextRaidAt = (float)NetworkTime.time + firstRaidDelay;
            _activeRaid = RaidKind.None;
            _initialized = true;
        }

        [ServerCallback]
        private void Update()
        {
            // Schedule + tick
            if (_activeRaid == RaidKind.None)
            {
                if ((float)NetworkTime.time >= _nextRaidAt) BeginRandomRaid();
            }
            else
            {
                TickActiveRaid();
            }
        }

        // ---------- Raid lifecycle ----------

        [Server]
        public void BeginRandomRaid()
        {
            // Pick: 50/50 player vs station, but fall back to player when no
            // stations exist.
            var stations = CraftingStation.ActiveStations;
            bool hasStations = false;
            for (int i = 0; i < stations.Count; i++) { if (stations[i] != null) { hasStations = true; break; } }

            var kind = hasStations && Random.value < 0.5f ? RaidKind.HuntStation : RaidKind.HuntPlayer;
            BeginRaid(kind);
        }

        [Server]
        public void BeginRaid(RaidKind kind)
        {
            if (draugrPrefab == null)
            {
                Debug.LogWarning("[Steading] RaidDirector has no draugrPrefab — re-run M2 Setup so it's wired.");
                _nextRaidAt = (float)NetworkTime.time + 60f;
                return;
            }

            Transform target = ResolveTarget(kind);
            if (target == null)
            {
                _nextRaidAt = (float)NetworkTime.time + 60f;
                return;
            }

            _activeRaid = kind;
            _activeTarget = target;
            _activeTargetName = target.name;
            _activeRaidStartedAt = (float)NetworkTime.time;
            _activeMobs.Clear();

            int n = Random.Range(minWarbandSize, maxWarbandSize + 1);
            var spawnOrigin = ChooseSpawnOrigin(target);
            for (int i = 0; i < n; i++)
            {
                var jitter = new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
                var pos = spawnOrigin + jitter;
                if (NavMesh.SamplePosition(pos, out var hit, 12f, NavMesh.AllAreas)) pos = hit.position;

                var mob = Instantiate(draugrPrefab, pos, Quaternion.LookRotation((target.position - pos).normalized, Vector3.up));
                NetworkServer.Spawn(mob);

                var ctrl = mob.GetComponent<EnemyController>();
                if (ctrl != null)
                {
                    ctrl.SetOverrideTarget(target);
                    _activeMobs.Add(ctrl);
                }
            }

            _activeRemaining = _activeMobs.Count;
            Debug.Log($"[Steading] Raid started: {kind} -> {_activeTargetName} ({_activeRemaining} mobs)");
        }

        [Server]
        private void TickActiveRaid()
        {
            // Count survivors
            int alive = 0;
            for (int i = _activeMobs.Count - 1; i >= 0; i--)
            {
                var m = _activeMobs[i];
                if (m == null) { _activeMobs.RemoveAt(i); continue; }
                var hp = m.GetComponent<Steading.Combat.Health>();
                if (hp == null || hp.IsDead) { _activeMobs.RemoveAt(i); continue; }
                alive++;
            }
            _activeRemaining = alive;

            // Win conditions: target destroyed, all mobs dead, or hard timeout
            bool targetGone = _activeTarget == null;
            bool stationDead = _activeRaid == RaidKind.HuntStation && IsTargetStationDestroyed();
            bool timedOut = (float)NetworkTime.time - _activeRaidStartedAt > raidEndTimeout;

            if (alive == 0 || targetGone || stationDead || timedOut)
            {
                EndRaid();
            }
        }

        [Server]
        private bool IsTargetStationDestroyed()
        {
            if (_activeTarget == null) return true;
            var hp = _activeTarget.GetComponent<Steading.Combat.Health>();
            return hp == null || hp.IsDead;
        }

        [Server]
        private void EndRaid()
        {
            // Clear override targets first so anything we don't destroy returns
            // to organic aggro instead of marching toward a stale raid target.
            for (int i = 0; i < _activeMobs.Count; i++)
            {
                if (_activeMobs[i] != null) _activeMobs[i].SetOverrideTarget(null);
            }
            // Then destroy survivors so we don't leak Draugr around the map.
            for (int i = _activeMobs.Count - 1; i >= 0; i--)
            {
                if (_activeMobs[i] != null) NetworkServer.Destroy(_activeMobs[i].gameObject);
            }
            _activeMobs.Clear();

            _activeRaid = RaidKind.None;
            _activeTargetName = string.Empty;
            _activeRemaining = 0;
            _activeTarget = null;
            _nextRaidAt = (float)NetworkTime.time + secondsBetweenRaids;

            Debug.Log("[Steading] Raid ended.");
        }

        // ---------- Target + spawn-origin selection ----------

        [Server]
        private Transform ResolveTarget(RaidKind kind)
        {
            switch (kind)
            {
                case RaidKind.HuntPlayer:
                {
                    Transform pick = null;
                    int n = 0;
                    foreach (var conn in NetworkServer.connections.Values)
                    {
                        if (conn?.identity == null) continue;
                        n++;
                        if (Random.Range(0, n) == 0) pick = conn.identity.transform; // reservoir-1
                    }
                    return pick;
                }
                case RaidKind.HuntStation:
                {
                    var stations = CraftingStation.ActiveStations;
                    var live = new List<CraftingStation>();
                    for (int i = 0; i < stations.Count; i++) if (stations[i] != null) live.Add(stations[i]);
                    if (live.Count == 0) return null;
                    return live[Random.Range(0, live.Count)].transform;
                }
            }
            return null;
        }

        [Server]
        private Vector3 ChooseSpawnOrigin(Transform target)
        {
            // Push spawn back along the vector from origin to target so mobs
            // come "from outside" the player's clearing. Capped to keep them
            // on the navigable map.
            var p = target.position;
            var fromCenter = new Vector3(p.x, 0f, p.z).normalized;
            if (fromCenter.sqrMagnitude < 0.01f) fromCenter = Random.insideUnitSphere; // edge case if target is at origin
            fromCenter.y = 0f;
            return p + fromCenter * spawnFromMapEdge;
        }
    }
}
