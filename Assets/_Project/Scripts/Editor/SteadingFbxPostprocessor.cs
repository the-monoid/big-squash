using System.IO;
using UnityEditor;
using UnityEngine;

namespace Steading.EditorTools
{
    // Auto-configures FBX/OBJ imports based on the Steading naming convention so
    // artists don't have to remember the avatar / animation / material settings
    // every time. Pairs with the Blender add-on (SourceArt/Blender/steading_addon.py)
    // which exports files with the matching name prefixes.
    //
    //   Player_*    -> Humanoid avatar, animations imported, generic mat extraction
    //   Enemy_*     -> Generic avatar (root motion-friendly), animations imported
    //   Weapon_*    -> No animation, mesh-only, optimized
    //   Buildable_* -> No animation, mesh-only, no normals (we recompute)
    //   World_*     -> No animation, mesh-only, baked normals kept
    //
    // Drop a file with one of those prefixes anywhere under
    //   Assets/_Project/Art/Models/
    // and Unity will configure it on import. After the asset finishes importing,
    // an extracted-materials pass swaps each created Material to use the painterly
    // shader so the model renders consistently with the rest of the world.
    public class SteadingFbxPostprocessor : AssetPostprocessor
    {
        private const string ModelsRootRelative = "Assets/_Project/Art/Models";
        private const string PlayerBaseFileName = "Player_VikingHero";
        private const string EnemyBaseFileName  = "Enemy_Draugr";
        private const string PlayerAvatarSubAssetPath = ModelsRootRelative + "/Characters/Player/Player_VikingHero.fbx";
        private const string EnemyAvatarSubAssetPath  = ModelsRootRelative + "/Characters/Enemies/Enemy_Draugr.fbx";

        // ------------------------------------------------- Pre-import settings

        private void OnPreprocessModel()
        {
            if (!IsSteadingModel(assetPath, out var category, out var isAnimated)) return;

            var importer = (ModelImporter)assetImporter;

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = (category == SteadingCategory.Player || category == SteadingCategory.Enemy);
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;

            switch (category)
            {
                case SteadingCategory.Player:
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.importAnimation = isAnimated;
                    importer.importNormals = ModelImporterNormals.Calculate;
                    // The rigged base mesh (Player_VikingHero) defines its OWN avatar.
                    // Every other Player_*.fbx is a Mixamo animation clip — it must
                    // copy the avatar from the base so Mecanim retargets the bones
                    // correctly. Without this, each anim FBX makes its own incompatible
                    // avatar and the player just T-poses.
                    var fnPlayer = Path.GetFileNameWithoutExtension(assetPath);
                    if (string.Equals(fnPlayer, PlayerBaseFileName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    }
                    else
                    {
                        var baseAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(PlayerAvatarSubAssetPath);
                        if (baseAvatar != null)
                        {
                            importer.sourceAvatar = baseAvatar;
                            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        }
                        else
                        {
                            // Base avatar isn't loaded yet (first-import order). Fall
                            // back to CreateFromThisModel; we re-apply on a follow-up
                            // pass via OnPostprocessAllAssets below.
                            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        }
                    }
                    break;
                case SteadingCategory.Enemy:
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.importAnimation = isAnimated;
                    importer.importNormals = ModelImporterNormals.Calculate;
                    var fnEnemy = Path.GetFileNameWithoutExtension(assetPath);
                    if (string.Equals(fnEnemy, EnemyBaseFileName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    }
                    else
                    {
                        var baseAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(EnemyAvatarSubAssetPath);
                        if (baseAvatar != null)
                        {
                            importer.sourceAvatar = baseAvatar;
                            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        }
                        else
                        {
                            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        }
                    }
                    break;
                case SteadingCategory.Weapon:
                case SteadingCategory.Buildable:
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importAnimation = false;
                    importer.importNormals = ModelImporterNormals.Calculate;
                    break;
                case SteadingCategory.World:
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importAnimation = false;
                    importer.importNormals = ModelImporterNormals.Import;
                    break;
            }
        }

        // ------------------------------------------------- Post-import (material swap)

        // For models, OnPostprocessMaterial runs once per material. Unity creates a
        // standard Lit material on import; we re-target it at the painterly shader
        // and copy across the base map / color so the model integrates with our
        // banded lighting + rim look automatically.
        public Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!IsSteadingModel(assetPath, out _, out _)) return null;

            var painterly = Shader.Find("Steading/PainterlyLit");
            if (painterly == null) return null;

            // Tell Unity we want it to create the material itself, but we'll
            // post-process it on the next import pass to swap the shader.
            return null;
        }

        public void OnPostprocessMaterial(Material material)
        {
            if (!IsSteadingModel(assetPath, out _, out _)) return;
            var painterly = Shader.Find("Steading/PainterlyLit");
            if (painterly == null) return;

            // Capture color/map before changing shader (some properties don't survive).
            Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                            : material.HasProperty("_Color")     ? material.GetColor("_Color")
                            : Color.white;
            Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap")
                            : material.HasProperty("_MainTex") ? material.GetTexture("_MainTex")
                            : null;

            material.shader = painterly;
            material.SetColor("_BaseColor", baseColor);
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);

            // Sensible painterly defaults for an imported asset (overridden once
            // someone tunes it manually).
            material.SetColor("_ShadowTint",    new Color(0.36f, 0.42f, 0.55f));
            material.SetColor("_MidtoneTint",   new Color(0.85f, 0.82f, 0.78f));
            material.SetColor("_HighlightTint", new Color(1.05f, 1.00f, 0.92f));
            material.SetColor("_RimColor",      new Color(1.10f, 0.85f, 0.50f));
            material.SetFloat("_RimIntensity",  0.55f);
            material.SetFloat("_RimPower",      3.6f);
            material.SetFloat("_AmbientStrength", 0.55f);
        }

        // ------------------------------------------------- Sweep extracted materials
        // OnPostprocessMaterial only fires for InPrefab materials. We use External
        // material extraction so artists can tweak per-asset, so we sweep the
        // imported folder after each import and retarget any standalone .mat
        // that isn't on the painterly shader yet.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // ---- Avatar copy race recovery ----
            // If the base rigged FBX (Player_VikingHero / Enemy_Draugr) was just
            // imported, force-reimport every sibling animation FBX so they pick up
            // the now-existing avatar. Otherwise the animations stay as their own
            // incompatible avatars and Mecanim retargeting fails (T-pose).
            ReapplyAvatarsIfBaseLanded(imported, PlayerAvatarSubAssetPath, PlayerBaseFileName);
            ReapplyAvatarsIfBaseLanded(imported, EnemyAvatarSubAssetPath,  EnemyBaseFileName);

            var painterly = Shader.Find("Steading/PainterlyLit");
            if (painterly == null) return;

            foreach (var path in imported)
            {
                if (!path.StartsWith(ModelsRootRelative)) continue;
                if (!path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == painterly) continue;

                var baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                              : mat.HasProperty("_Color")     ? mat.GetColor("_Color")
                              : Color.white;
                var baseMap   = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap")
                              : mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex")
                              : null;

                mat.shader = painterly;
                mat.SetColor("_BaseColor", baseColor);
                if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);

                mat.SetColor("_ShadowTint",    new Color(0.36f, 0.42f, 0.55f));
                mat.SetColor("_MidtoneTint",   new Color(0.85f, 0.82f, 0.78f));
                mat.SetColor("_HighlightTint", new Color(1.05f, 1.00f, 0.92f));
                mat.SetColor("_RimColor",      new Color(1.10f, 0.85f, 0.50f));
                mat.SetFloat("_RimIntensity",  0.55f);
                mat.SetFloat("_RimPower",      3.6f);
                mat.SetFloat("_AmbientStrength", 0.55f);

                EditorUtility.SetDirty(mat);
            }
        }

        private static void ReapplyAvatarsIfBaseLanded(string[] imported, string baseFbxPath, string baseFileName)
        {
            bool baseImported = false;
            foreach (var p in imported)
            {
                if (p.Replace('\\', '/').Equals(baseFbxPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    baseImported = true;
                    break;
                }
            }
            if (!baseImported) return;

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(baseFbxPath);
            if (avatar == null) return;

            // Find every sibling animation FBX in the same folder.
            var folder = Path.GetDirectoryName(baseFbxPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return;

            var siblings = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (var guid in siblings)
            {
                var siblingPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(siblingPath)) continue;
                if (siblingPath.Equals(baseFbxPath, System.StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(siblingPath);
                if (string.Equals(name, baseFileName, System.StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(siblingPath) as ModelImporter;
                if (importer == null) continue;
                if (importer.sourceAvatar == avatar &&
                    importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther) continue;

                importer.sourceAvatar = avatar;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------- Helpers

        private enum SteadingCategory { Player, Enemy, Weapon, Buildable, World }

        private static bool IsSteadingModel(string path, out SteadingCategory category, out bool isAnimated)
        {
            category = SteadingCategory.World;
            isAnimated = false;

            if (string.IsNullOrEmpty(path)) return false;

            var normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith(ModelsRootRelative)) return false;

            var fileName = Path.GetFileNameWithoutExtension(normalized);
            if (string.IsNullOrEmpty(fileName)) return false;

            if (fileName.StartsWith("Player_",    System.StringComparison.OrdinalIgnoreCase)) { category = SteadingCategory.Player;    isAnimated = true;  return true; }
            if (fileName.StartsWith("Enemy_",     System.StringComparison.OrdinalIgnoreCase)) { category = SteadingCategory.Enemy;     isAnimated = true;  return true; }
            if (fileName.StartsWith("Weapon_",    System.StringComparison.OrdinalIgnoreCase)) { category = SteadingCategory.Weapon;    isAnimated = false; return true; }
            if (fileName.StartsWith("Buildable_", System.StringComparison.OrdinalIgnoreCase)) { category = SteadingCategory.Buildable; isAnimated = false; return true; }
            if (fileName.StartsWith("World_",     System.StringComparison.OrdinalIgnoreCase)) { category = SteadingCategory.World;     isAnimated = false; return true; }

            return false;
        }
    }
}
