using Mirror;
using UnityEngine;

namespace Steading.Player
{
    public enum ResourceKind
    {
        Wood = 0,
        Stone = 1,
    }

    public class PlayerInventory : NetworkBehaviour
    {
        [SyncVar] private int _wood;
        [SyncVar] private int _stone;

        public int Wood => _wood;
        public int Stone => _stone;

        [Server]
        public void Add(ResourceKind kind, int amount)
        {
            if (amount <= 0) return;

            switch (kind)
            {
                case ResourceKind.Wood:
                    _wood += amount;
                    break;
                case ResourceKind.Stone:
                    _stone += amount;
                    break;
            }
        }

        private void OnGUI()
        {
            if (!isLocalPlayer) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
            };

            var bg = new Rect(16f, Screen.height - 100f, 170f, 70f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, Screen.height - 92f, 150f, 24f), "Resources", style);
            GUI.Label(new Rect(28f, Screen.height - 68f, 150f, 24f), $"Wood: {_wood}", style);
            GUI.Label(new Rect(28f, Screen.height - 45f, 150f, 24f), $"Stone: {_stone}", style);
        }
    }
}
