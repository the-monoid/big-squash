using System.Collections.Generic;
using Mirror;
using Steading.Combat;
using Steading.Player;
using UnityEngine;

namespace Steading.World
{
    public class ResourceNode : MonoBehaviour
    {
        private static readonly Dictionary<string, ResourceNode> Nodes = new Dictionary<string, ResourceNode>();

        [SerializeField] private string nodeId;
        [SerializeField] private ResourceKind resourceKind = ResourceKind.Wood;
        [SerializeField] private int maxHealth = 60;
        [SerializeField] private int resourceYield = 6;
        [SerializeField] private WeaponKind requiredWeapon = WeaponKind.Axe;
        [SerializeField] private float fallDistance = 0.9f;
        [SerializeField] private float fallRotation = 76f;

        private int _health;
        private bool _depleted;
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        public string NodeId => nodeId;
        public bool IsDepleted => _depleted;

        private void Awake()
        {
            if (string.IsNullOrEmpty(nodeId)) nodeId = gameObject.name;
            _health = maxHealth;
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            Nodes[nodeId] = this;
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(nodeId) && Nodes.TryGetValue(nodeId, out var node) && node == this)
            {
                Nodes.Remove(nodeId);
            }
        }

        public static bool TryFind(string id, out ResourceNode node)
        {
            return Nodes.TryGetValue(id, out node);
        }

        public void Configure(string id, ResourceKind kind, int hp, int resourceYield, WeaponKind weapon)
        {
            if (!string.IsNullOrEmpty(nodeId) && Nodes.TryGetValue(nodeId, out var existing) && existing == this)
            {
                Nodes.Remove(nodeId);
            }

            nodeId = id;
            resourceKind = kind;
            maxHealth = Mathf.Max(1, hp);
            this.resourceYield = Mathf.Max(1, resourceYield);
            requiredWeapon = weapon;
            _health = maxHealth;
            Nodes[nodeId] = this;
        }

        [Server]
        public bool ApplyToolHit(WeaponKind weapon, int power, NetworkIdentity source, out int awarded)
        {
            awarded = 0;
            if (_depleted || power <= 0) return false;
            if (weapon != requiredWeapon) return false;

            _health = Mathf.Max(0, _health - power);

            if (_health > 0) return false;

            _depleted = true;
            awarded = resourceYield;

            var inventory = source != null ? source.GetComponent<PlayerInventory>() : null;
            if (inventory != null) inventory.Add(resourceKind, awarded);
            return true;
        }

        public void ApplyClientHit(bool depleted)
        {
            if (depleted)
            {
                _depleted = true;
                transform.position = _startPosition + transform.forward * fallDistance + Vector3.down * 0.25f;
                transform.rotation = _startRotation * Quaternion.Euler(fallRotation, 0f, 0f);

                foreach (var collider in GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
                return;
            }

            StopAllCoroutines();
            StartCoroutine(HitPulse());
        }

        private System.Collections.IEnumerator HitPulse()
        {
            var original = transform.localScale;
            transform.localScale = original * 1.035f;
            yield return new WaitForSeconds(0.055f);
            transform.localScale = original;
        }
    }
}
