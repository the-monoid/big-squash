using UnityEngine;

namespace Steading.World
{
    [DisallowMultipleComponent]
    public class WalkableSurface : MonoBehaviour
    {
        [SerializeField] private float snapOffset;

        public float SnapOffset => snapOffset;
    }
}
