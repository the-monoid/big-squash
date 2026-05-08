using System.IO;
using UnityEditor;
using UnityEngine;

namespace Steading.EditorTools
{
    // Force-retargets every Player_*/Enemy_* animation FBX so its avatar
    // is "Copy From Other Avatar" pointing at the rigged base
    // (Player_VikingHero / Enemy_Draugr).
    //
    // Why this is a separate menu: the SteadingFbxPostprocessor does this on
    // import, but only if the base FBX is already in the project. If the user
    // dragged-and-dropped all FBX simultaneously, the import order is
    // non-deterministic and animations can land first. They then create their
    // own avatars and Mecanim retargeting silently fails — the character stays
    // in T-pose while the controller plays clips it can't apply.
    //
    // Run this once after Phase 0 imports settle, then re-run the Animator
    // builder + M1 setup.
    public static class AnimationAvatarRetargetTool
    {
        private const string PlayerDir = "Assets/_Project/Art/Models/Characters/Player";
        private const string EnemyDir  = "Assets/_Project/Art/Models/Characters/Enemies";
        private const string PlayerBase = "Player_VikingHero";
        private const string EnemyBase  = "Enemy_Draugr";

        [MenuItem("Steading/Animator: Retarget All Mixamo FBX Avatars")]
        public static void Retarget()
        {
            int playerCount = RetargetFolder(PlayerDir, PlayerBase, isHumanoid: true);
            int enemyCount  = RetargetFolder(EnemyDir,  EnemyBase,  isHumanoid: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Avatar Retarget",
                $"Retargeted {playerCount} player + {enemyCount} enemy animation FBX.\n\n" +
                "Now re-run:\n" +
                "  1. Steading > Animator: Build PlayerAnimator Controller\n" +
                "  2. Steading > M1: Generate Bootstrap, World, and Player\n\n" +
                "Then Play > Host. The character should animate instead of T-posing.",
                "OK");
        }

        private static int RetargetFolder(string folder, string baseFileName, bool isHumanoid)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return 0;

            var basePath = $"{folder}/{baseFileName}.fbx";
            var baseAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(basePath);
            if (baseAvatar == null)
            {
                Debug.LogWarning($"[Steading] Retarget: base avatar not found at '{basePath}'. " +
                                 "Make sure the rigged base FBX imported with Animation Type = Humanoid (or Generic for enemies).");
                return 0;
            }

            // Force the base to CreateFromThisModel so its avatar is canonical.
            var baseImporter = AssetImporter.GetAtPath(basePath) as ModelImporter;
            if (baseImporter != null)
            {
                baseImporter.animationType = isHumanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
                baseImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                baseImporter.SaveAndReimport();
                baseAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(basePath);
            }

            int n = 0;
            var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Equals(basePath, System.StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                importer.animationType = isHumanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
                importer.sourceAvatar = baseAvatar;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.importAnimation = true;
                importer.SaveAndReimport();
                n++;
            }
            return n;
        }
    }
}
