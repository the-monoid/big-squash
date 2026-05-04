using Mirror;
using UnityEngine;

namespace Steading.Combat
{
    // M2 placeholder melee. Left-click triggers a server-side raycast from the
    // local player's camera and applies damage to the first Health component
    // hit within range. Replaced in M2 phase 2 by proper Weapon classes with
    // animation-driven hitboxes.
    public class PlayerAttack : NetworkBehaviour
    {
        // Range needs to cover camera-to-player distance + reach in third-person.
        // 6m supports our default cameraOffset of -3.5z plus ~2.5m attack reach.
        [SerializeField] private float range = 6f;
        [SerializeField] private int damage = 25;
        [SerializeField] private float cooldown = 0.4f;
        [SerializeField] private LayerMask hitLayers = ~0;

        private float _nextSwingTime;

        private void Update()
        {
            if (!isLocalPlayer) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (Time.time < _nextSwingTime) return;

            _nextSwingTime = Time.time + cooldown;

            var cam = Camera.main;
            if (cam == null) return;

            CmdAttack(cam.transform.position, cam.transform.forward);
        }

        [Command]
        private void CmdAttack(Vector3 origin, Vector3 dir)
        {
            // Sanity check: don't trust an arbitrary origin blindly. Allow up to
            // 5m from the player so the third-person camera origin is valid; tighter
            // anti-cheat lands in M2 phase 2 alongside proper Weapon classes.
            if (Vector3.Distance(origin, transform.position) > 5f) return;

            // RaycastAll + sort + first non-self: the camera ray passes through
            // the player's own collider in third-person before reaching enemies.
            var hits = Physics.RaycastAll(origin, dir.normalized, range, hitLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<Health>();
                if (target == null) continue;
                if (target.gameObject == gameObject) continue;

                target.TakeDamage(new DamageInfo
                {
                    amount = damage,
                    hitPoint = hit.point,
                    hitDirection = dir.normalized,
                    sourceNetId = netId,
                });
                return;
            }
        }
    }
}
