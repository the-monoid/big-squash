using Mirror;
using UnityEngine;

namespace Steading.Player
{
    public enum ResourceKind
    {
        Wood = 0,
        Stone = 1,
        Bronze = 2,
        Iron = 3,
        Steel = 4,
    }

    public class PlayerInventory : NetworkBehaviour
    {
        [SyncVar] private int _wood;
        [SyncVar] private int _stone;
        [SyncVar] private int _bronze;
        [SyncVar] private int _iron;
        [SyncVar] private int _steel;

        public int Wood   => _wood;
        public int Stone  => _stone;
        public int Bronze => _bronze;
        public int Iron   => _iron;
        public int Steel  => _steel;

        [Server]
        public void Add(ResourceKind kind, int amount)
        {
            if (amount <= 0) return;
            switch (kind)
            {
                case ResourceKind.Wood:   _wood   += amount; break;
                case ResourceKind.Stone:  _stone  += amount; break;
                case ResourceKind.Bronze: _bronze += amount; break;
                case ResourceKind.Iron:   _iron   += amount; break;
                case ResourceKind.Steel:  _steel  += amount; break;
            }
        }

        public int GetAmount(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Wood:   return _wood;
                case ResourceKind.Stone:  return _stone;
                case ResourceKind.Bronze: return _bronze;
                case ResourceKind.Iron:   return _iron;
                case ResourceKind.Steel:  return _steel;
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

        [Server]
        public bool TrySpend(System.Collections.Generic.IList<Steading.Building.ResourceCost> cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost == null) return true;
            for (int i = 0; i < cost.Count; i++)
            {
                switch (cost[i].kind)
                {
                    case ResourceKind.Wood:   _wood   -= cost[i].amount; break;
                    case ResourceKind.Stone:  _stone  -= cost[i].amount; break;
                    case ResourceKind.Bronze: _bronze -= cost[i].amount; break;
                    case ResourceKind.Iron:   _iron   -= cost[i].amount; break;
                    case ResourceKind.Steel:  _steel  -= cost[i].amount; break;
                }
            }
            return true;
        }

        private void OnGUI()
        {
            if (!isLocalPlayer) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
            };

            var bg = new Rect(16f, Screen.height - 168f, 180f, 152f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, Screen.height - 162f, 160f, 22f), "Resources", style);
            GUI.Label(new Rect(28f, Screen.height - 140f, 160f, 22f), $"Wood:   {_wood}",   style);
            GUI.Label(new Rect(28f, Screen.height - 118f, 160f, 22f), $"Stone:  {_stone}",  style);
            GUI.Label(new Rect(28f, Screen.height -  96f, 160f, 22f), $"Bronze: {_bronze}", style);
            GUI.Label(new Rect(28f, Screen.height -  74f, 160f, 22f), $"Iron:   {_iron}",   style);
            GUI.Label(new Rect(28f, Screen.height -  52f, 160f, 22f), $"Steel:  {_steel}",  style);
        }
    }
}
