using Mirror;
using UnityEngine;

namespace Steading.Combat
{
    [RequireComponent(typeof(Health))]
    public class PlayerRespawn : NetworkBehaviour
    {
        [SerializeField] private float respawnDelay = 2f;

        private Health _health;
        private float _respawnAt;
        private bool _waitingToRespawn;

        private void Awake() => _health = GetComponent<Health>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        [ServerCallback]
        private void Update()
        {
            if (!_waitingToRespawn || Time.time < _respawnAt) return;
            _waitingToRespawn = false;
            DoRespawn();
        }

        [Server]
        private void OnDied()
        {
            _waitingToRespawn = true;
            _respawnAt = Time.time + respawnDelay;
        }

        [Server]
        private void DoRespawn()
        {
            var spawn = NetworkManager.startPositions.Count > 0
                ? NetworkManager.startPositions[Random.Range(0, NetworkManager.startPositions.Count)]
                : null;

            var pos = spawn != null ? spawn.position : Vector3.zero;
            var rot = spawn != null ? spawn.rotation : Quaternion.identity;

            // CharacterController must be disabled when teleporting or it overrides the move.
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.SetPositionAndRotation(pos, rot);
            RpcSnapToTransform(pos, rot);

            if (cc != null) cc.enabled = true;

            _health.ResetToFull();
        }

        [ClientRpc]
        private void RpcSnapToTransform(Vector3 pos, Quaternion rot)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            if (cc != null) cc.enabled = true;
        }
    }
}
