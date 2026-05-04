using UnityEngine;

namespace Steading.Combat
{
    public struct DamageInfo
    {
        public int amount;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
        public uint sourceNetId;
    }
}
