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

        public int GetAmount(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Wood:  return _wood;
                case ResourceKind.Stone: return _stone;
            }
            return 0;
        }

        public bool CanAfford(System.Collections.Generic.IList<Steading.Building.ResourceCost> cost)
        {
            if (cost == null || cost.Count == 0) return true;
            for (int i = 0; i < cost.Count; i++)
            {
                if (GetAmount(cost[i].kind) < cost[i].amount) return false;
            }
            return true;
        }

        // Atomic deduct — returns true and spends if affordable, otherwise no-op.
        [Server]
        public bool TrySpend(System.Collections.Generic.IList<Steading.Building.ResourceCost> cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost == null) return true;

            for (int i = 0; i < cost.Count; i++)
            {
                switch (cost[i].kind)
                {
                    case ResourceKind.Wood:  _wood  -= cost[i].amount; break;
                    case ResourceKind.Stone: _stone -= cost[i].amount; break;
                }
            }
            return true;
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
