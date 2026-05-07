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
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = isAnimated;
                    importer.importNormals = ModelImporterNormals.Calculate;
                    break;
                case SteadingCategory.Enemy:
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = isAnimated;
                    importer.importNormals = ModelImporterNormals.Calculate;
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
