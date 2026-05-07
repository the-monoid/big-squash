using System;
using UnityEngine;

namespace Steading.Building
{
    [Serializable]
    public struct Socket
    {
        [Tooltip("Tag identifying which sockets this is. BuildController matches a buildable's snapTags[] against this.")]
        public string tag;

        [Tooltip("Position relative to the host structure where the placed buildable's center should land.")]
        public Vector3 localPosition;

        [Tooltip("Rotation (Euler degrees) the placed buildable should adopt when snapping here.")]
        public Vector3 localEuler;

        public Quaternion LocalRotation => Quaternion.Euler(localEuler);
    }

    // Drop on a placed buildable prefab. Each entry tells BuildController:
    // "if you have a buildable whose snapTags[] contains tag X, snap it to my
    // localPosition/localEuler instead of using grid snap."
    public class StructureSockets : MonoBehaviour
    {
        [SerializeField] private Socket[] sockets;

        public Socket[] Sockets => sockets ?? System.Array.Empty<Socket>();

        public bool TryGetWorldSocket(int index, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = Vector3.zero;
            worldRot = Quaternion.identity;
            if (sockets == null || index < 0 || index >= sockets.Length) return false;

            var s = sockets[index];
            worldPos = transform.TransformPoint(s.localPosition);
            worldRot = transform.rotation * s.LocalRotation;
            return true;
        }

        // Helper used only at edit time / in M3Setup to populate sockets without
        // hand-authoring them in the inspector.
        public void SetSockets(Socket[] s) => sockets = s;
    }
}
