using System.Collections.Generic;
using Mirror;
using Steading.Combat;
using Steading.World;
using UnityEngine;

namespace Steading.Building
{
    // Marks a placed buildable. Listens for its own Health.Died and despawns.
    // Also runs a small server-side support graph: structures are supported if
    // they touch static ground or connect through adjacent structures to ground.
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class Structure : NetworkBehaviour
    {
        private static readonly HashSet<Structure> ServerStructures = new HashSet<Structure>();
        private static readonly HashSet<Structure> Supported = new HashSet<Structure>();
        private static readonly Queue<Structure> Frontier = new Queue<Structure>();
        private static readonly List<Structure> Snapshot = new List<Structure>();
        private static bool _solving;

        [Header("Integrity")]
        [SerializeField] private float supportPadding = 0.15f;
        [SerializeField] private float groundProbeDepth = 0.12f;
        [SerializeField] private LayerMask supportLayers = ~0;

        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (GetComponent<BuildableVisualEnhancer>() == null)
            {
                gameObject.AddComponent<BuildableVisualEnhancer>();
            }

            if (name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                GetComponent<WalkableSurface>() == null)
            {
                gameObject.AddComponent<WalkableSurface>();
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerStructures.Add(this);
            _health.Died += OnDiedServer;
            RecalculateSupportGraph();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerStructures.Remove(this);
            if (_health != null) _health.Died -= OnDiedServer;
            if (NetworkServer.active) RecalculateSupportGraph();
        }

        [Server]
        private void OnDiedServer()
        {
            NetworkServer.Destroy(gameObject);
        }

        [Server]
        private static void RecalculateSupportGraph()
        {
            if (_solving) return;
            _solving = true;

            try
            {
                Supported.Clear();
                Frontier.Clear();
                Snapshot.Clear();
                Snapshot.AddRange(ServerStructures);

                foreach (var structure in Snapshot)
                {
                    if (structure == null) continue;
                    if (!structure.HasGroundContact()) continue;

                    Supported.Add(structure);
                    Frontier.Enqueue(structure);
                }

                while (Frontier.Count > 0)
                {
                    var current = Frontier.Dequeue();
                    foreach (var candidate in Snapshot)
                    {
                        if (candidate == null || Supported.Contains(candidate)) continue;
                        if (!current.Touches(candidate)) continue;

                        Supported.Add(candidate);
                        Frontier.Enqueue(candidate);
                    }
                }

                foreach (var structure in Snapshot)
                {
                    if (structure == null || Supported.Contains(structure)) continue;
                    NetworkServer.Destroy(structure.gameObject);
                }
            }
            finally
            {
                _solving = false;
            }
        }

        private bool HasGroundContact()
        {
            if (!TryGetWorldBounds(out var bounds)) return true;

            var center = new Vector3(bounds.center.x, bounds.min.y - groundProbeDepth * 0.5f, bounds.center.z);
            var extents = new Vector3(
                Mathf.Max(0.05f, bounds.extents.x * 0.9f),
                groundProbeDepth,
                Mathf.Max(0.05f, bounds.extents.z * 0.9f));

            var hits = Physics.OverlapBox(center, extents, Quaternion.identity, supportLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<Structure>() != null) continue;
                if (hit.GetComponentInParent<NetworkIdentity>() != null) continue;
                if (hit.attachedRigidbody != null) continue;
                return true;
            }

            return false;
        }

        private bool Touches(Structure other)
        {
            if (!TryGetWorldBounds(out var a) || !other.TryGetWorldBounds(out var b)) return false;
            a.Expand(supportPadding * 2f);
            b.Expand(other.supportPadding * 2f);
            return a.Intersects(b);
        }

        private bool TryGetWorldBounds(out Bounds bounds)
        {
            var colliders = GetComponentsInChildren<Collider>();
            bounds = default(Bounds);
            var initialized = false;

            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                if (!initialized)
                {
                    bounds = col.bounds;
                    initialized = true;
                    continue;
                }
                bounds.Encapsulate(col.bounds);
            }

            return initialized;
        }
    }
}
