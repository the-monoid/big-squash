using Mirror;
using UnityEngine;

namespace Steading.Building
{
    // Local-player build mode. Press B to toggle. Ghost preview snaps to a
    // grid where the camera ray hits the world; left-click sends a CmdPlace
    // with position+rotation to the server for authoritative spawn.
    // Right-click on an existing Structure deletes it (server-side).
    public class BuildController : NetworkBehaviour
    {
        [Header("Buildables")]
        [SerializeField] private GameObject[] buildables;
        [Tooltip("Half-extents per buildable (for placement overlap and ghost shape). " +
                 "Index aligns with buildables[]. Defaults to (1, 1.5, 0.1) — wall-shaped.")]
        [SerializeField] private Vector3[] buildableHalfExtents;

        [Header("Placement")]
        [SerializeField] private float maxRange = 5f;
        [SerializeField] private float gridSize = 1f;
        [SerializeField] private LayerMask placementLayers = ~0;

        [Header("Ghost visuals")]
        [SerializeField] private Material ghostValidMat;
        [SerializeField] private Material ghostInvalidMat;

        private bool _inBuildMode;
        private int _selectedIndex;
        private GameObject _ghost;
        private MeshRenderer _ghostRenderer;

        public bool InBuildMode => _inBuildMode;

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
            if (!_inBuildMode && _ghost != null)
            {
                _ghost.SetActive(false);
            }
        }

        private void CycleSelected()
        {
            if (buildables == null || buildables.Length == 0) return;
            _selectedIndex = (_selectedIndex + 1) % buildables.Length;
            // Force ghost shape refresh on next Update.
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
            // Total ray length = max placement reach plus camera-behind-player offset.
            var totalDist = maxRange + Vector3.Distance(cam.transform.position, transform.position);
            var hits = Physics.RaycastAll(ray, totalDist, placementLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // First non-self hit decides. Camera ray passes through the local
            // player's own collider in third-person before reaching the ground.
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

            // Reject placing on dynamic or networked objects (other players, walls, etc.).
            if (hit.collider.attachedRigidbody != null) return false;
            if (hit.collider.GetComponentInParent<NetworkIdentity>() != null) return false;

            var snapped = hit.point;
            snapped.x = Mathf.Round(snapped.x / gridSize) * gridSize;
            snapped.z = Mathf.Round(snapped.z / gridSize) * gridSize;
            // Lift so the buildable's bottom sits on the hit surface.
            snapped.y = hit.point.y + halfExtents.y;

            var yaw = Mathf.Round(transform.eulerAngles.y / 90f) * 90f;
            pos = snapped;
            rot = Quaternion.Euler(0f, yaw, 0f);
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
                if (st == null) return false; // first non-self hit isn't a structure
                target = st.netIdentity;
                return true;
            }
            return false;
        }

        private void UpdateGhost(bool hasHit, Vector3 pos, Quaternion rot, Vector3 halfExtents)
        {
            EnsureGhost(halfExtents);
            if (!hasHit)
            {
                _ghost.SetActive(false);
                return;
            }

            _ghost.SetActive(true);
            _ghost.transform.SetPositionAndRotation(pos, rot);

            var overlap = Physics.CheckBox(pos, halfExtents * 0.95f, rot, ~0, QueryTriggerInteraction.Ignore);
            if (_ghostRenderer != null)
            {
                _ghostRenderer.sharedMaterial = overlap ? ghostInvalidMat : ghostValidMat;
            }
        }

        private void EnsureGhost(Vector3 halfExtents)
        {
            if (_ghost != null) return;
            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "BuildGhost";
            Destroy(_ghost.GetComponent<Collider>());
            _ghost.transform.localScale = halfExtents * 2f;
            _ghostRenderer = _ghost.GetComponent<MeshRenderer>();
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostRenderer.receiveShadows = false;
            _ghost.SetActive(false);
        }

        private Vector3 GetHalfExtents(int index)
        {
            if (buildableHalfExtents != null && index < buildableHalfExtents.Length)
            {
                var e = buildableHalfExtents[index];
                if (e.sqrMagnitude > 0.001f) return e;
            }
            return new Vector3(1f, 1.5f, 0.1f);
        }

        [Command]
        private void CmdPlace(int index, Vector3 pos, Quaternion rot)
        {
            if (buildables == null || index < 0 || index >= buildables.Length) return;
            var prefab = buildables[index];
            if (prefab == null) return;

            // Server-side sanity check: position must be within reach of the player.
            if (Vector3.Distance(pos, transform.position) > maxRange + 4f) return;

            // Refuse placement that would clip an existing collider (other than the ground).
            var halfExtents = GetHalfExtents(index);
            var overlaps = Physics.OverlapBox(pos, halfExtents * 0.9f, rot, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in overlaps)
            {
                if (c.gameObject.name == "Ground") continue;
                return;
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
