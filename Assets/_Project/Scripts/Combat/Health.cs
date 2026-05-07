using System;
using Mirror;
using UnityEngine;

namespace Steading.Combat
{
    public class Health : NetworkBehaviour
    {
        [SerializeField] private int maxHp = 100;

        [SyncVar(hook = nameof(OnHpChanged))]
        private int _hp;

        public int Hp => _hp;
        public int MaxHp => maxHp;
        public bool IsDead => _hp <= 0;

        public event Action<int, int> HpChanged;     // (oldHp, newHp), fires on every client+server
        public event Action<DamageInfo> Damaged;     // server-only; fires when TakeDamage applies
        public event Action Died;                    // server-only; fires once when hp first hits 0

        public override void OnStartServer()
        {
            base.OnStartServer();
            _hp = maxHp;
        }

        [Server]
        public void TakeDamage(DamageInfo info)
        {
            if (_hp <= 0 || info.amount <= 0) return;

            var playerAttack = GetComponent<PlayerAttack>();
            if (playerAttack != null && playerAttack.TryMitigateIncomingDamage(ref info))
            {
                if (info.amount <= 0) return;
            }

            _hp = Mathf.Max(0, _hp - info.amount);
            Damaged?.Invoke(info);

            if (_hp == 0) Died?.Invoke();
        }

        [Server]
        public void Heal(int amount)
        {
            if (_hp <= 0 || amount <= 0) return;
            _hp = Mathf.Min(maxHp, _hp + amount);
        }

        [Server]
        public void ResetToFull()
        {
            _hp = maxHp;
        }

        [Server]
        public void SetMaxHpRuntime(int value, bool refill)
        {
            maxHp = Mathf.Max(1, value);
            if (refill || _hp <= 0) _hp = maxHp;
            else _hp = Mathf.Min(_hp, maxHp);
        }

        private void OnHpChanged(int oldHp, int newHp)
        {
            HpChanged?.Invoke(oldHp, newHp);
        }
    }
}
