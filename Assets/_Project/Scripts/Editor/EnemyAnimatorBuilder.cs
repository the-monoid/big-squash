using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Steading.EditorTools
{
    // Builds EnemyAnimator.controller from the imported Mixamo Mutant clips
    // AND modifies Draugr.prefab to use the Enemy_Draugr.fbx visual driven
    // by EnemyAnimatorBridge.
    //
    // Pairs with the existing AnimationAvatarRetargetTool (which sets loop
    // flags + Generic avatar copy from Enemy_Draugr.fbx for every Enemy_*.fbx).
    public static class EnemyAnimatorBuilder
    {
        private const string EnemyDir = "Assets/_Project/Art/Models/Characters/Enemies";
        private const string AnimDir = "Assets/_Project/Animation";
        private const string ControllerPath = AnimDir + "/EnemyAnimator.controller";
        private const string DraugrPrefabPath = "Assets/_Project/Prefabs/Draugr.prefab";
        private const string DraugrFbxPath = EnemyDir + "/Enemy_Draugr.fbx";

        private static readonly (string fbxName, string param)[] ClipMap =
        {
            ("Enemy_Idle",        "Idle"),
            ("Enemy_Walk",        "Walk"),
            ("Enemy_Run",         "Run"),
            ("Enemy_Attack",      "Attack"),
            ("Enemy_HeavyAttack", "HeavyAttack"),
            ("Enemy_JumpAttack",  "JumpAttack"),
            ("Enemy_HitReact",    "HitReact"),
            ("Enemy_Death",       "Death"),
        };

        [MenuItem("Steading/Animator: Build Enemy Animator + Swap Draugr Visual")]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Enemy Animator Builder",
                    "Stop Play mode and try again.", "OK");
                return;
            }

            EnsureFolder(AnimDir);

            var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (var (fbxName, param) in ClipMap)
            {
                var clip = LoadFirstClipFromFbx($"{EnemyDir}/{fbxName}.fbx");
                if (clip != null) clips[param] = clip;
            }

            // Need at least Idle to ship a controller.
            if (!clips.ContainsKey("Idle"))
            {
                EditorUtility.DisplayDialog("Enemy Animator Builder",
                    "Enemy_Idle.fbx not found or has no AnimationClip.\n" +
                    "Re-run Steading > Animator: Retarget All Mixamo FBX Avatars and try again.",
                    "OK");
                return;
            }

            var controller = BuildController(clips);
            SwapDraugrPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Enemy Animator Builder",
                $"Built EnemyAnimator.controller ({clips.Count} clips wired) and " +
                "swapped Draugr.prefab to use Enemy_Draugr.fbx visual + EnemyAnimatorBridge.\n\n" +
                "Run M2 Setup again to spawn a fresh Draugr in World_Test.",
                "OK");
        }

        // ---------- Controller ----------

        private static AnimatorController BuildController(System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed",       AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack",      AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpAttack",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitReact",    AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die",         AnimatorControllerParameterType.Trigger);

            BuildLocomotion(controller, clips);
            BuildCombat(controller, clips);
            BuildReaction(controller, clips);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildLocomotion(AnimatorController controller, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var baseLayer = controller.layers[0];
            baseLayer.name = "Locomotion";
            controller.layers = controller.layers;

            var sm = baseLayer.stateMachine;

            var blend = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };
            blend.AddChild(clips["Idle"], 0f);
            if (clips.TryGetValue("Walk", out var walkClip)) blend.AddChild(walkClip, 1.5f);
            if (clips.TryGetValue("Run",  out var runClip))  blend.AddChild(runClip,  4.0f);
            AssetDatabase.AddObjectToAsset(blend, controller);

            var loco = sm.AddState("Locomotion");
            loco.motion = blend;
            sm.defaultState = loco;
        }

        private static void BuildCombat(AnimatorController controller, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var combat = new AnimatorControllerLayer
            {
                name = "Combat",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "Combat", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(combat.stateMachine, controller);
            controller.AddLayer(combat);

            var sm = combat.stateMachine;
            var idle = sm.AddState("Empty");
            sm.defaultState = idle;

            AddTriggered(sm, idle, clips, "Attack",      "Attack");
            AddTriggered(sm, idle, clips, "HeavyAttack", "HeavyAttack");
            AddTriggered(sm, idle, clips, "JumpAttack",  "JumpAttack");
        }

        private static void BuildReaction(AnimatorController controller, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var react = new AnimatorControllerLayer
            {
                name = "Reaction",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "Reaction", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(react.stateMachine, controller);
            controller.AddLayer(react);

            var sm = react.stateMachine;
            var idle = sm.AddState("Empty");
            sm.defaultState = idle;

            AddTriggered(sm, idle, clips, "HitReact", "HitReact");

            if (clips.TryGetValue("Death", out var deathClip))
            {
                var death = sm.AddState("Death");
                death.motion = deathClip;
                death.writeDefaultValues = false;

                var enter = idle.AddTransition(death);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                enter.duration = 0.10f;
                enter.hasExitTime = false;
            }
        }

        private static void AddTriggered(AnimatorStateMachine sm, AnimatorState idle,
            System.Collections.Generic.Dictionary<string, AnimationClip> clips,
            string clipKey, string triggerName)
        {
            if (!clips.TryGetValue(clipKey, out var clip)) return;

            var state = sm.AddState(clipKey);
            state.motion = clip;
            state.speed = 1f;
            state.writeDefaultValues = false;

            var enter = idle.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            enter.duration = 0.18f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;
            enter.interruptionSource = TransitionInterruptionSource.Source;

            var exit = state.AddTransition(idle);
            exit.duration = 0.22f;
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
        }

        // ---------- Draugr.prefab swap ----------

        private static void SwapDraugrPrefab(RuntimeAnimatorController controller)
        {
            var draugrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DraugrPrefabPath);
            if (draugrPrefab == null)
            {
                Debug.LogWarning($"[Steading] Draugr.prefab not found at {DraugrPrefabPath} — run M2 Setup first to generate it, then re-run this menu.");
                return;
            }

            var mutantFbx = AssetDatabase.LoadAssetAtPath<GameObject>(DraugrFbxPath);
            if (mutantFbx == null)
            {
                Debug.LogWarning($"[Steading] Enemy_Draugr.fbx not found at {DraugrFbxPath} — Phase 0 import incomplete.");
                return;
            }

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(DraugrFbxPath);

            using var edit = new PrefabUtility.EditPrefabContentsScope(DraugrPrefabPath);
            var root = edit.prefabContentsRoot;

            // Remove old visual children (any existing VisualRig or capsule fallbacks).
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child.GetComponent<Animator>() != null ||
                    child.name.StartsWith("Visual") ||
                    child.name.StartsWith("Mutant") ||
                    child.name.StartsWith("Enemy_"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Spawn the imported Mutant FBX as a child of Draugr root.
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(mutantFbx, root.transform);
            visual.name = "VisualRig_Mutant";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Replace the procedural EnemyVisualAnimator with the Mecanim bridge.
            var oldVisualAnimator = root.GetComponent<Steading.AI.EnemyVisualAnimator>();
            if (oldVisualAnimator != null) Object.DestroyImmediate(oldVisualAnimator);
            if (root.GetComponent<Steading.AI.EnemyAnimatorBridge>() == null)
            {
                root.AddComponent<Steading.AI.EnemyAnimatorBridge>();
            }
        }

        // ---------- Helpers ----------

        private static AnimationClip LoadFirstClipFromFbx(string fbxPath)
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), fbxPath))) return null;
            var sub = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
            foreach (var s in sub)
            {
                if (s is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
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
