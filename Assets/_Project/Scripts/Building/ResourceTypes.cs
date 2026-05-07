using System;
using UnityEngine;

namespace Steading.Building
{
    public enum ResourceKind
    {
        Wood = 0,
        Stone = 1,
        Iron = 2,
        Hide = 3,
    }

    [Serializable]
    public struct ResourceCost
    {
        public ResourceKind kind;
        public int amount;
    }
}
