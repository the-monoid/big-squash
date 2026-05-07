using System;
using Steading.Player;

namespace Steading.Building
{
    // Cost line item for a buildable. References Steading.Player.ResourceKind
    // (the canonical enum, owned by PlayerInventory) so the wallet and the
    // build cost line up automatically.
    [Serializable]
    public struct ResourceCost
    {
        public ResourceKind kind;
        public int amount;
    }
}
