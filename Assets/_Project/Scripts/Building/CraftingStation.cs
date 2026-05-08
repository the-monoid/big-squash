using System.Collections.Generic;
using Mirror;
using Steading.Combat;
using Steading.Player;
using UnityEngine;

namespace Steading.Building
{
    // Drop on a Workbench buildable. Within 2.5m, the local player can press E
    // to open the crafting panel and consume resources to craft a higher-tier
    // weapon. Server-authoritative spend; client UI is request-only.
    //
    // Stations register themselves with a static list so RaidDirector can pick
    // a random station as a raid target.
    [RequireComponent(typeof(NetworkIdentity))]
    public class CraftingStation : NetworkBehaviour
    {
        private static readonly List<CraftingStation> All = new List<CraftingStation>();
        public static IReadOnlyList<CraftingStation> ActiveStations => All;

        [SerializeField] private float interactRadius = 2.5f;
        public float InteractRadius => interactRadius;

        public override void OnStartServer()
        {
            base.OnStartServer();
            All.Add(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            All.Remove(this);
        }

        // Server validates and either succeeds or no-ops. Caller is the player
        // (uses sender connection) — only their inventory is touched.
        [Command(requiresAuthority = false)]
        public void CmdCraft(int weaponDefIndex, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.identity == null) return;
            var def = WeaponLibrary.Instance.GetByIndex(weaponDefIndex);
            if (def == null) return;
            if (def.starter) return; // can't "craft" a starter — already owned

            var playerObj = sender.identity.gameObject;
            var inventory = playerObj.GetComponent<PlayerInventory>();
            var attack = playerObj.GetComponent<PlayerAttack>();
            if (inventory == null || attack == null) return;

            // Range check (server-side)
            if (Vector3.Distance(playerObj.transform.position, transform.position) > interactRadius + 1.5f) return;

            if (!inventory.TrySpend(def.cost)) return;
            attack.ServerUnlockWeapon(weaponDefIndex);
        }
    }
}
