using UnityEditor;
using UnityEngine;

namespace Steading.EditorTools
{
    // Replaces the Mixamo X Bot visual in Player.prefab with the Synty
    // POLYGON Starter male character (SM_Chr_Male_01). Mixamo-authored
    // animations retarget cleanly onto Synty's humanoid avatar via Mecanim.
    //
    // Solves the "skin not loaded" issue — Synty ships with proper textures
    // baked into the character mesh; X Bot is a placeholder white mesh.
    //
    // Idempotent: re-running deletes the previous visual child and rebuilds.
    public static class PlayerVisualSwap
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string SyntyCharPrefabPath =
            "Assets/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01.prefab";
        private const string SyntyCharFbxPath =
            "Assets/Synty/PolygonStarter/Models/Characters.fbx";
        private const string PlayerAnimatorPath =
            "Assets/_Project/Animation/PlayerAnimator.controller";

        [MenuItem("Steading/Art: Swap Player Visual to Synty Viking")]
        public static void Swap()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Player Visual Swap",
                    "Stop Play mode and try again.", "OK");
                return;
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("Player Visual Swap",
                    "Player.prefab not found. Run M1: Generate Bootstrap, World, and Player first.", "OK");
                return;
            }

            var syntyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SyntyCharPrefabPath);
            if (syntyPrefab == null)
            {
                EditorUtility.DisplayDialog("Player Visual Swap",
                    $"Synty character not found at:\n{SyntyCharPrefabPath}\n\n" +
                    "Make sure the POLYGON Starter Pack is imported.", "OK");
                return;
            }

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(SyntyCharFbxPath);
            if (avatar == null)
            {
                Debug.LogWarning("[Steading] Synty avatar not found at Characters.fbx — Mecanim retargeting may fail. " +
                                 "Open Characters.fbx in Inspector → Rig → Animation Type = Humanoid → Apply.");
            }

            using (var edit = new PrefabUtility.EditPrefabContentsScope(PlayerPrefabPath))
            {
                var root = edit.prefabContentsRoot;

                // Remove any existing visual children (VisualRig from Mixamo, or
                // VisualFallback capsule, or any prior Synty instance).
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child.GetComponent<Animator>() != null ||
                        child.name.StartsWith("Visual") ||
                        child.name.StartsWith("SM_Chr"))
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                // Instantiate Synty as a child of Player.
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(syntyPrefab, root.transform);
                visual.name = "VisualRig_Synty";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                // Wire the Animator: Mecanim humanoid retargets automatically
                // from the Mixamo-authored clips in PlayerAnimator.controller
                // onto Synty's avatar.
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
                if (controller != null) animator.runtimeAnimatorController = controller;
                if (avatar != null) animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Player Visual Swap",
                "Swapped to Synty SM_Chr_Male_01.\n\n" +
                "Player.prefab now uses the Synty humanoid mesh; Mixamo animations " +
                "retarget at runtime via Mecanim. Press Play > Host to verify.",
                "OK");
        }
    }
}
