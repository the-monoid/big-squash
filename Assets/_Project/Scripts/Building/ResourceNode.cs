using Mirror;
using Steading.Combat;
using UnityEngine;

namespace Steading.Building
{
    // Drop on a tree, rock, ore vein etc. Hits from PlayerAttack damage the node;
    // when its Health.Died fires, the killing player's wallet gets the loot.
    [RequireComponent(typeof(Health))]
    public class ResourceNode : NetworkBehaviour
    {
        [SerializeField] private ResourceKind kind = ResourceKind.Wood;
        [SerializeField] private int amount = 5;
        [SerializeField] private bool respawn = true;
        [SerializeField] private float respawnSeconds = 60f;

        private Health _health;
        private uint _lastAttackerNetId;
        private float _respawnAt;
        private bool _waitingForRespawn;
        private Vector3 _initialPos;
        private Quaternion _initialRot;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _initialPos = transform.position;
            _initialRot = transform.rotation;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Damaged += OnDamagedServer;
            _health.Died    += OnDiedServer;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_health != null)
            {
                _health.Damaged -= OnDamagedServer;
                _health.Died    -= OnDiedServer;
            }
        }

        [Server]
        private void OnDamagedServer(DamageInfo info) => _lastAttackerNetId = info.sourceNetId;

        [Server]
        private void OnDiedServer()
        {
            if (NetworkServer.spawned.TryGetValue(_lastAttackerNetId, out var attackerNi) && attackerNi != null)
            {
                var wallet = attackerNi.GetComponent<ResourceWallet>();
                if (wallet != null) wallet.AddResource(kind, amount);
            }

            if (respawn)
            {
                gameObject.SetActive(false);
                _waitingForRespawn = true;
                _respawnAt = Time.time + respawnSeconds;
            }
            else
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        [ServerCallback]
        private void Update()
        {
            if (!_waitingForRespawn || Time.time < _respawnAt) return;
            _waitingForRespawn = false;
            transform.SetPositionAndRotation(_initialPos, _initialRot);
            _health.ResetToFull();
            gameObject.SetActive(true);
        }
    }
}
