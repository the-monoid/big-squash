using Mirror;
using Steading.Combat;
using UnityEngine;

namespace Steading.Building
{
    // Marks a placed buildable. Listens for its own Health.Died and despawns.
    // M3 phase 2 will add structural-integrity solving (foundation graph,
    // cascade collapse on support loss) — for now, walls just disappear when
    // their HP hits zero.
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class Structure : NetworkBehaviour
    {
        private Health _health;

        private void Awake() => _health = GetComponent<Health>();

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

        [Server]
        private void OnDiedServer()
        {
            NetworkServer.Destroy(gameObject);
        }
    }
}
