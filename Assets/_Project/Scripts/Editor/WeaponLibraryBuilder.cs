using System.Collections.Generic;
using Steading.Building;
using Steading.Combat;
using Steading.Player;
using UnityEditor;
using UnityEngine;

namespace Steading.EditorTools
{
    // Generates the canonical WeaponLibrary asset + 8 WeaponDef SOs (Wood,
    // Bronze, Iron, Steel × Sword, Axe). Idempotent — re-running tunes the
    // existing assets in place rather than duplicating.
    //
    // The library lives at Assets/_Project/Resources/WeaponLibrary.asset so
    // WeaponLibrary.Instance.Resources.Load picks it up at runtime.
    public static class WeaponLibraryBuilder
    {
        private const string ResourcesDir = "Assets/_Project/Resources";
        private const string DataDir      = "Assets/_Project/Data/Weapons";
        private const string LibraryPath  = ResourcesDir + "/WeaponLibrary.asset";

        [MenuItem("Steading/Combat: Generate Default Weapon Library")]
        public static void Build()
        {
            EnsureFolder(ResourcesDir);
            EnsureFolder(DataDir);

            var library = AssetDatabase.LoadAssetAtPath<WeaponLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<WeaponLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.weapons.Clear();

            // ---- Sword tiers ----
            library.weapons.Add(MakeWeapon("WoodSword",         WeaponKind.Sword, WeaponTier.Wood,
                lightDmg: 18, heavyDmg: 32, lightCD: 0.42f, heavyCD: 0.74f, range: 2.4f,
                cost: System.Array.Empty<ResourceCost>(), starter: true,
                blade: new Color(0.62f, 0.46f, 0.30f), grip: new Color(0.4f, 0.28f, 0.14f)));

            library.weapons.Add(MakeWeapon("BronzeSword",       WeaponKind.Sword, WeaponTier.Bronze,
                lightDmg: 28, heavyDmg: 46, lightCD: 0.40f, heavyCD: 0.70f, range: 2.5f,
                cost: Cost((ResourceKind.Wood, 3), (ResourceKind.Bronze, 4)),
                blade: new Color(0.85f, 0.55f, 0.20f), grip: new Color(0.36f, 0.22f, 0.10f)));

            library.weapons.Add(MakeWeapon("IronSword",         WeaponKind.Sword, WeaponTier.Iron,
                lightDmg: 38, heavyDmg: 60, lightCD: 0.38f, heavyCD: 0.66f, range: 2.6f,
                cost: Cost((ResourceKind.Wood, 5), (ResourceKind.Iron, 6)),
                blade: new Color(0.78f, 0.80f, 0.84f), grip: new Color(0.20f, 0.16f, 0.12f)));

            library.weapons.Add(MakeWeapon("RusticSteelSword",  WeaponKind.Sword, WeaponTier.Steel,
                lightDmg: 52, heavyDmg: 82, lightCD: 0.36f, heavyCD: 0.62f, range: 2.7f,
                cost: Cost((ResourceKind.Wood, 8), (ResourceKind.Iron, 4), (ResourceKind.Steel, 6)),
                // "Rustic steel" — slightly oxidized cold blue-grey blade, dark wrapped grip.
                blade: new Color(0.62f, 0.66f, 0.72f), grip: new Color(0.14f, 0.10f, 0.08f)));

            // ---- Axe tiers ----
            library.weapons.Add(MakeWeapon("WoodAxe",           WeaponKind.Axe, WeaponTier.Wood,
                lightDmg: 14, heavyDmg: 26, lightCD: 0.55f, heavyCD: 0.90f, range: 2.2f,
                cost: System.Array.Empty<ResourceCost>(), starter: true,
                blade: new Color(0.75f, 0.55f, 0.30f), grip: new Color(0.4f, 0.26f, 0.12f)));

            library.weapons.Add(MakeWeapon("BronzeAxe",         WeaponKind.Axe, WeaponTier.Bronze,
                lightDmg: 22, heavyDmg: 38, lightCD: 0.52f, heavyCD: 0.85f, range: 2.3f,
                cost: Cost((ResourceKind.Wood, 4), (ResourceKind.Bronze, 5)),
                blade: new Color(0.85f, 0.55f, 0.20f), grip: new Color(0.36f, 0.22f, 0.10f)));

            library.weapons.Add(MakeWeapon("IronAxe",           WeaponKind.Axe, WeaponTier.Iron,
                lightDmg: 32, heavyDmg: 52, lightCD: 0.48f, heavyCD: 0.80f, range: 2.4f,
                cost: Cost((ResourceKind.Wood, 6), (ResourceKind.Iron, 8)),
                blade: new Color(0.78f, 0.80f, 0.84f), grip: new Color(0.20f, 0.16f, 0.12f)));

            library.weapons.Add(MakeWeapon("RusticSteelAxe",    WeaponKind.Axe, WeaponTier.Steel,
                lightDmg: 44, heavyDmg: 70, lightCD: 0.46f, heavyCD: 0.76f, range: 2.5f,
                cost: Cost((ResourceKind.Wood, 10), (ResourceKind.Iron, 6), (ResourceKind.Steel, 8)),
                blade: new Color(0.62f, 0.66f, 0.72f), grip: new Color(0.14f, 0.10f, 0.08f)));

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Weapon Library",
                $"Built WeaponLibrary with {library.weapons.Count} entries (8 weapons).\n\n" +
                "Wood tiers are starter (auto-equipped on spawn).\n" +
                "Bronze/Iron/Steel require crafting at a Workbench.",
                "OK");
        }

        private static WeaponDef MakeWeapon(string name, WeaponKind kind, WeaponTier tier,
            int lightDmg, int heavyDmg, float lightCD, float heavyCD, float range,
            ResourceCost[] cost, Color blade, Color grip, bool starter = false)
        {
            var path = $"{DataDir}/{name}.asset";
            var def = AssetDatabase.LoadAssetAtPath<WeaponDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<WeaponDef>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.displayName    = name;
            def.kind           = kind;
            def.tier           = tier;
            def.damageLight    = lightDmg;
            def.damageHeavy    = heavyDmg;
            def.cooldownLight  = lightCD;
            def.cooldownHeavy  = heavyCD;
            def.range          = range;
            def.cost           = cost;
            def.starter        = starter;
            def.bladeTint      = blade;
            def.gripTint       = grip;
            EditorUtility.SetDirty(def);
            return def;
        }

        // Compile-time-typed cost factory. `Cost((Wood, 3), (Bronze, 4))`
        // produces a ResourceCost[] without any unchecked boxing/casts.
        private static ResourceCost[] Cost(params (ResourceKind kind, int amount)[] pairs)
        {
            var list = new List<ResourceCost>(pairs.Length);
            for (int i = 0; i < pairs.Length; i++)
            {
                list.Add(new ResourceCost { kind = pairs[i].kind, amount = pairs[i].amount });
            }
            return list.ToArray();
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var slash = assetPath.LastIndexOf('/');
            var parent = slash >= 0 ? assetPath.Substring(0, slash) : "Assets";
            var name = slash >= 0 ? assetPath.Substring(slash + 1) : assetPath;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
