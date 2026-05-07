using System;
using Mirror;
using UnityEngine;

namespace Steading.Building
{
    // Per-player resource wallet. Server-authoritative. Clients see their own
    // wallet via SyncVar replication so the BuildHud can render bars.
    //
    // Stored as 4 separate SyncVars (one per ResourceKind) rather than a
    // SyncDictionary so server-side spend math stays a single tick.
    public class ResourceWallet : NetworkBehaviour
    {
        [SerializeField] private int startingWood = 30;
        [SerializeField] private int startingStone = 10;

        [SyncVar] private int _wood;
        [SyncVar] private int _stone;
        [SyncVar] private int _iron;
        [SyncVar] private int _hide;

        public int Wood  => _wood;
        public int Stone => _stone;
        public int Iron  => _iron;
        public int Hide  => _hide;

        public int GetAmount(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Wood:  return _wood;
                case ResourceKind.Stone: return _stone;
                case ResourceKind.Iron:  return _iron;
                case ResourceKind.Hide:  return _hide;
            }
            return 0;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _wood = startingWood;
            _stone = startingStone;
        }

        [Server]
        public void AddResource(ResourceKind kind, int amount)
        {
            if (amount <= 0) return;
            switch (kind)
            {
                case ResourceKind.Wood:  _wood  = Mathf.Clamp(_wood  + amount, 0, 9999); break;
                case ResourceKind.Stone: _stone = Mathf.Clamp(_stone + amount, 0, 9999); break;
                case ResourceKind.Iron:  _iron  = Mathf.Clamp(_iron  + amount, 0, 9999); break;
                case ResourceKind.Hide:  _hide  = Mathf.Clamp(_hide  + amount, 0, 9999); break;
            }
        }

        [Server]
        public bool CanAfford(ResourceCost[] cost)
        {
            if (cost == null || cost.Length == 0) return true;
            foreach (var c in cost)
            {
                if (GetAmount(c.kind) < c.amount) return false;
            }
            return true;
        }

        // Atomic deduct — returns true and spends if affordable, otherwise no-op.
        [Server]
        public bool TrySpend(ResourceCost[] cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost == null) return true;

            foreach (var c in cost)
            {
                switch (c.kind)
                {
                    case ResourceKind.Wood:  _wood  -= c.amount; break;
                    case ResourceKind.Stone: _stone -= c.amount; break;
                    case ResourceKind.Iron:  _iron  -= c.amount; break;
                    case ResourceKind.Hide:  _hide  -= c.amount; break;
                }
            }
            return true;
        }
    }
}
