using System;
using Mirror;
using UnityEngine;

namespace Steading.Building
{
    [Serializable]
    public struct BuildableEntry
    {
        public string label;
        public GameObject prefab;
        [Tooltip("Half-extents used for ghost preview shape and overlap-rejection check.")]
        public Vector3 halfExtents;
    }

    // Local-player build mode. B toggles. Tab cycles type. R rotates 90°.
    // Camera ray + 1m grid snap drives the ghost preview (translucent green
    // when valid, red when overlapping). Left-click sends CmdPlace; right-
    // click on an existing Structure sends CmdDelete.
    public class BuildController : NetworkBehaviour
    {
        [Header("Buildables")]
        [SerializeField] private BuildableEntry[] buildables;

        [Header("Placement")]
        [SerializeField] private float maxRange = 5f;
        [SerializeField] private float gridSize = 1f;
        [SerializeField] private LayerMask placementLayers = ~0;

        [Header("Ghost visuals")]
        [SerializeField] private Material ghostValidMat;
        [SerializeField] private Material ghostInvalidMat;

        private bool _inBuildMode;
        private int _selectedIndex;
        private float _placeYawOffsetDeg;
        private GameObject _ghost;
        private MeshRenderer _ghostRenderer;
        private Vector3 _ghostExtentsApplied;

        public bool InBuildMode => _inBuildMode;
        public string CurrentBuildableLabel =>
            (buildables != null && _selectedIndex < buildables.Length)
                ? buildables[_selectedIndex].label
                : null;

        private void OnDestroy()
        {
            if (_ghost != null) Destroy(_ghost);
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            if (Input.GetKeyDown(KeyCode.B)) ToggleBuildMode();
            if (!_inBuildMode) return;

            if (Input.GetKeyDown(KeyCode.Tab)) CycleSelected();
            if (Input.GetKeyDown(KeyCode.R)) _placeYawOffsetDeg = (_placeYawOffsetDeg + 90f) % 360f;

            var cam = Camera.main;
            if (cam == null) return;

            var hit = TryFindPlacement(cam, out var pos, out var rot, out var halfExtents);
            UpdateGhost(hit, pos, rot, halfExtents);

            if (hit && Input.GetMouseButtonDown(0))
            {
                CmdPlace(_selectedIndex, pos, rot);
            }

            if (Input.GetMouseButtonDown(1))
            {
                if (TryRaycastStructure(cam, out var target))
                {
                    CmdDelete(target);
                }
            }
        }

        private void ToggleBuildMode()
        {
            _inBuildMode = !_inBuildMode;
            if (!_inBuildMode && _ghost != null) _ghost.SetActive(false);
        }

        private void CycleSelected()
        {
            if (buildables == null || buildables.Length == 0) return;
            _selectedIndex = (_selectedIndex + 1) % buildables.Length;
            // Force ghost shape refresh: extent change requires a new primitive.
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRenderer = null;
        }

        private bool TryFindPlacement(Camera cam, out Vector3 pos, out Quaternion rot, out Vector3 halfExtents)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            halfExtents = GetHalfExtents(_selectedIndex);

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var totalDist = maxRange + Vector3.Distance(cam.transform.position, transform.position);
            var hits = Physics.RaycastAll(ray, totalDist, placementLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // First non-self hit decides. Camera ray passes through the local
            // player's own collider in third-person before reaching the world.
            RaycastHit? chosen = null;
            foreach (var h in hits)
            {
                var ni = h.collider.GetComponentInParent<NetworkIdentity>();
                if (ni != null && ni == netIdentity) continue;
                chosen = h;
                break;
            }
            if (!chosen.HasValue) return false;
            var hit = chosen.Value;

            // Reject placement on dynamic things (rigidbodies). Allow placement on
            // static colliders (Ground) AND on existing static structures (so you
            // can stack a floor on a wall). Reject networked non-Structure things
            // — i.e., other players or Draugr.
            if (hit.collider.attachedRigidbody != null) return false;
            var hitNi = hit.collider.GetComponentInParent<NetworkIdentity>();
            if (hitNi != null && hit.collider.GetComponentInParent<Structure>() == null) return false;

            var snapped = hit.point;
            snapped.x = Mathf.Round(snapped.x / gridSize) * gridSize;
            snapped.z = Mathf.Round(snapped.z / gridSize) * gridSize;
            // Lift so the buildable's bottom sits on the hit surface.
            snapped.y = hit.point.y + halfExtents.y;

            var baseYaw = Mathf.Round(transform.eulerAngles.y / 90f) * 90f;
            pos = snapped;
            rot = Quaternion.Euler(0f, baseYaw + _placeYawOffsetDeg, 0f);
            return true;
        }

        private bool TryRaycastStructure(Camera cam, out NetworkIdentity target)
        {
            target = null;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var totalDist = maxRange + Vector3.Distance(cam.transform.position, transform.position);
            var hits = Physics.RaycastAll(ray, totalDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                var ni = h.collider.GetComponentInParent<NetworkIdentity>();
                if (ni != null && ni == netIdentity) continue;
                var st = h.collider.GetComponentInParent<Structure>();
                if (st == null) return false;
                target = st.netIdentity;
                return true;
            }
            return false;
        }

        private void UpdateGhost(bool hasHit, Vector3 pos, Quaternion rot, Vector3 halfExtents)
        {
            EnsureGhost(halfExtents);
            if (!hasHit) { _ghost.SetActive(false); return; }

            _ghost.SetActive(true);
            _ghost.transform.SetPositionAndRotation(pos, rot);

            var overlap = WouldOverlap(pos, halfExtents, rot);
            if (_ghostRenderer != null)
            {
                _ghostRenderer.sharedMaterial = overlap ? ghostInvalidMat : ghostValidMat;
            }
        }

        private bool WouldOverlap(Vector3 pos, Vector3 halfExtents, Quaternion rot)
        {
            // Slight inset so two adjacent buildables snapping to the grid don't
            // mutually fail their own overlap checks against each other's edges.
            var probe = halfExtents * 0.85f;
            var cols = Physics.OverlapBox(pos, probe, rot, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (c.gameObject.name == "Ground") continue;
                var ni = c.GetComponentInParent<NetworkIdentity>();
                if (ni != null && ni == netIdentity) continue;
                return true;
            }
            return false;
        }

        private void EnsureGhost(Vector3 halfExtents)
        {
            if (_ghost != null && _ghostExtentsApplied == halfExtents) return;
            if (_ghost != null) Destroy(_ghost);

            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "BuildGhost";
            Destroy(_ghost.GetComponent<Collider>());
            _ghost.transform.localScale = halfExtents * 2f;
            _ghostRenderer = _ghost.GetComponent<MeshRenderer>();
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostRenderer.receiveShadows = false;
            _ghostExtentsApplied = halfExtents;
            _ghost.SetActive(false);
        }

        private Vector3 GetHalfExtents(int index)
        {
            if (buildables != null && index >= 0 && index < buildables.Length)
            {
                var e = buildables[index].halfExtents;
                if (e.sqrMagnitude > 0.001f) return e;
            }
            return new Vector3(1f, 1.5f, 0.1f);
        }

        [Command]
        private void CmdPlace(int index, Vector3 pos, Quaternion rot)
        {
            if (buildables == null || index < 0 || index >= buildables.Length) return;
            var prefab = buildables[index].prefab;
            if (prefab == null) return;

            if (Vector3.Distance(pos, transform.position) > maxRange + 4f) return;

            var halfExtents = GetHalfExtents(index);
            var overlaps = Physics.OverlapBox(pos, halfExtents * 0.85f, rot, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in overlaps)
            {
                if (c.gameObject.name == "Ground") continue;
                var ni = c.GetComponentInParent<NetworkIdentity>();
                if (ni != null && ni == netIdentity) continue;
                return; // overlaps something else — reject
            }

            var instance = Instantiate(prefab, pos, rot);
            NetworkServer.Spawn(instance);
        }

        [Command]
        private void CmdDelete(NetworkIdentity target)
        {
            if (target == null) return;
            if (target.GetComponent<Structure>() == null) return;
            if (Vector3.Distance(target.transform.position, transform.position) > maxRange + 4f) return;
            NetworkServer.Destroy(target.gameObject);
        }
    }
}
