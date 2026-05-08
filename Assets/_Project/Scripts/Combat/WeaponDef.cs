using Steading.Building;
using UnityEngine;

namespace Steading.Combat
{
    public enum WeaponTier
    {
        Wood = 0,
        Bronze = 1,
        Iron = 2,
        Steel = 3,
    }

    // ScriptableObject definition for a single weapon. Each tier of a weapon
    // family (Sword/Axe) gets one of these. PlayerAttack reads damage,
    // cooldown, range, and resource cost from here so adding a new tier is
    // creating a new asset, not editing PlayerAttack.
    [CreateAssetMenu(menuName = "Steading/WeaponDef", fileName = "Weapon")]
    public class WeaponDef : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Wood Sword";
        public WeaponKind kind = WeaponKind.Sword;
        public WeaponTier tier = WeaponTier.Wood;

        [Header("Combat stats")]
        public int damageLight = 30;
        public int damageHeavy = 48;
        public float cooldownLight = 0.40f;
        public float cooldownHeavy = 0.70f;
        public float range = 2.4f;
        public float radius = 0.95f;

        [Header("Crafting")]
        [Tooltip("Resources consumed at the workbench to craft this weapon. Wood-tier weapons start unlocked, others require crafting.")]
        public ResourceCost[] cost;
        [Tooltip("True for the starter weapon — the player has it equipped on spawn without crafting.")]
        public bool starter;

        [Header("Visual tint")]
        [Tooltip("Multiplier applied to the procedural blade material so each tier reads at a glance.")]
        public Color bladeTint = Color.white;
        [Tooltip("Multiplier on the grip for tier differentiation (steel = darker leather, bronze = rusty etc.).")]
        public Color gripTint = Color.white;
    }
}
