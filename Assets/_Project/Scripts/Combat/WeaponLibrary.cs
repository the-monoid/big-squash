using System.Collections.Generic;
using UnityEngine;

namespace Steading.Combat
{
    // Singleton registry of WeaponDef ScriptableObjects. Loaded from a
    // Resources/WeaponLibrary asset at first access. Crafting + equip use
    // integer indices into this list so SyncVars + RPCs can reference weapons
    // without serializing the SO directly (Mirror can't sync ScriptableObject
    // references).
    [CreateAssetMenu(menuName = "Steading/WeaponLibrary", fileName = "WeaponLibrary")]
    public class WeaponLibrary : ScriptableObject
    {
        private static WeaponLibrary _instance;

        public static WeaponLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<WeaponLibrary>("WeaponLibrary");
                if (_instance == null)
                {
                    Debug.LogError("[Steading] WeaponLibrary not found at Resources/WeaponLibrary. " +
                                   "Run Steading > Combat: Generate Default Weapon Library to create it.");
                }
                return _instance;
            }
        }

        [Tooltip("Ordered list of every weapon in the game. Index = weapon ID used by crafting + SyncVars.")]
        public List<WeaponDef> weapons = new List<WeaponDef>();

        public WeaponDef GetByIndex(int index)
        {
            if (index < 0 || index >= weapons.Count) return null;
            return weapons[index];
        }

        public int IndexOf(WeaponDef def)
        {
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i] == def) return i;
            return -1;
        }

        public IEnumerable<int> StarterIndices()
        {
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i] != null && weapons[i].starter) yield return i;
        }
    }
}
