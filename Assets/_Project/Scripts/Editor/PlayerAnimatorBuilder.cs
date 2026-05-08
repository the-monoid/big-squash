using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Steading.EditorTools
{
    // Builds the PlayerAnimator.controller programmatically so artists never have
    // to wire the state machine in the Animator window. Produces:
    //
    //   Layer 0 — Base / Locomotion
    //     Locomotion (BlendTree, threshold = Speed)
    //       Idle      0.0 m/s
    //       Walk      1.5 m/s
    //       Run       4.5 m/s
    //     Jump        (triggered by !Grounded → returns to Locomotion when grounded)
    //
    //   Layer 1 — Combat (additive, weight 1)
    //     Empty (default)
    //     Slash         (trigger: Slash)
    //     Combo         (trigger: Combo)
    //     ShieldRush    (trigger: ShieldRush)
    //     PowerBash     (trigger: PowerBash)
    //     Block         (bool: Block — loops while held)
    //
    //   Layer 2 — Reaction (override, weight 1, mask body)
    //     Empty (default)
    //     HitReact      (trigger: HitReact)
    //     Death         (trigger: Die — terminal, no exit)
    //
    // Re-runnable. Re-running deletes the asset and rebuilds.
    public static class PlayerAnimatorBuilder
    {
        private const string PlayerDir = "Assets/_Project/Art/Models/Characters/Player";
        private const string AnimDir   = "Assets/_Project/Animation";
        private const string ControllerPath = AnimDir + "/PlayerAnimator.controller";

        // Animation clip names — extracted from each Mixamo FBX. Mixamo FBX files
        // store the clip name from the source ("mixamo.com" by default). We'll
        // search for whichever clip exists at the conventional path.
        private static readonly (string fbxName, string param)[] ClipMap =
        {
            ("Player_Idle",             "Idle"),
            ("Player_Walk",             "Walk"),
            ("Player_Run",              "Run"),
            ("Player_Jump",             "Jump"),
            ("Player_SwordSlash",       "Slash"),
            ("Player_SwordCombo",       "Combo"),
            ("Player_ShieldRush",       "ShieldRush"),
            ("Player_PowerBashCharge",  "PowerBashCharge"),
            ("Player_PowerBash",        "PowerBash"),
            ("Player_HitReact",         "HitReact"),
            ("Player_Block",            "Block"),
            ("Player_Death",            "Death"),
        };

        [MenuItem("Steading/Animator: Build PlayerAnimator Controller")]
        public static void Build()
        {
            EnsureFolder(AnimDir);

            // Pre-fetch every clip we need.
            var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (var (fbxName, param) in ClipMap)
            {
                var clip = LoadFirstClipFromFbx($"{PlayerDir}/{fbxName}.fbx");
                if (clip == null)
                {
                    Debug.LogWarning($"[Steading] No AnimationClip found in {PlayerDir}/{fbxName}.fbx — skipping {param}.");
                    continue;
                }
                clips[param] = clip;
            }

            if (!clips.ContainsKey("Idle") || !clips.ContainsKey("Walk") || !clips.ContainsKey("Run"))
            {
                EditorUtility.DisplayDialog(
                    "Player Animator Builder",
                    "Need at least Idle, Walk, Run clips to build the locomotion blend tree.\n" +
                    "Make sure Player_Idle.fbx, Player_Walk.fbx, Player_Run.fbx are imported.",
                    "OK");
                return;
            }

            // Clear out any existing controller so we rebuild from scratch.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // ----- Parameters -----
            controller.AddParameter("Speed",       AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalVel", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded",    AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouch",      AnimatorControllerParameterType.Bool);
            controller.AddParameter("Block",       AnimatorControllerParameterType.Bool);
            controller.AddParameter("Slash",       AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Combo",       AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ShieldRush",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("PowerBash",   AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Mine",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Chop",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitReact",    AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die",         AnimatorControllerParameterType.Trigger);

            BuildLocomotionLayer(controller, clips);
            BuildCombatLayer(controller, clips);
            BuildReactionLayer(controller, clips);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Player Animator Builder",
                $"Built PlayerAnimator.controller with {clips.Count} clips wired.\n\n" +
                "Drop it on the Player_VikingHero Animator component (or run the M1 Setup menu — that wires it automatically).",
                "OK");
        }

        // ----------------------------------------------- Layer 0: Locomotion

        private static void BuildLocomotionLayer(AnimatorController controller, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var baseLayer = controller.layers[0];
            baseLayer.name = "Locomotion";
            controller.layers = controller.layers; // assign back to apply rename

            var sm = baseLayer.stateMachine;

            // ---- BlendTree state ----
            var blend = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };
            blend.AddChild(clips["Idle"], 0f);
            blend.AddChild(clips["Walk"], 1.5f);
            blend.AddChild(clips["Run"],  4.5f);
            AssetDatabase.AddObjectToAsset(blend, controller);

            var locomotionState = sm.AddState("Locomotion");
            locomotionState.motion = blend;
            sm.defaultState = locomotionState;

            // ---- Jump state ----
            if (clips.TryGetValue("Jump", out var jumpClip))
            {
                var jumpState = sm.AddState("Jump");
                jumpState.motion = jumpClip;
                jumpState.speed = 1.0f;

                var locoToJump = locomotionState.AddTransition(jumpState);
                locoToJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                locoToJump.duration = 0.18f;            // smoother takeoff
                locoToJump.hasExitTime = false;
                locoToJump.canTransitionToSelf = false;

                var jumpToLoco = jumpState.AddTransition(locomotionState);
                jumpToLoco.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                jumpToLoco.duration = 0.22f;            // ease into landing recovery
                jumpToLoco.hasExitTime = false;
            }
        }

        // ----------------------------------------------- Layer 1: Combat (additive)

        private static void BuildCombatLayer(AnimatorController controller, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var combat = new AnimatorControllerLayer
            {
                name = "Combat",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
                stateMachine = new AnimatorStateMachine { name = "Combat", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(combat.stateMachine, controller);
            controller.AddLayer(combat);

            var sm = combat.stateMachine;
            var idle = sm.AddState("Empty");
            sm.defaultState = idle;

            AddTriggeredState(controller, sm, idle, clips, "Slash",      "Slash",      lookAtTrigger: true);
            AddTriggeredState(controller, sm, idle, clips, "Combo",      "Combo",      lookAtTrigger: true);
            AddTriggeredState(controller, sm, idle, clips, "ShieldRush", "ShieldRush", lookAtTrigger: true);
            AddTriggeredState(controller, sm, idle, clips, "PowerBash",  "PowerBash",  lookAtTrigger: true);

            // Tool-swing variants reuse the PowerBash overhead arc clip but
            // hang off their own triggers so PlayerAttack can route mining
            // input independently from sword combat. If a dedicated chop or
            // mine FBX lands later it slots in by extending ClipMap.
            AddTriggeredStateAlias(controller, sm, idle, clips, "PowerBash", "Mine", "Mine");
            AddTriggeredStateAlias(controller, sm, idle, clips, "PowerBash", "Chop", "Chop");

            // Block: held loop with bool driving it
            if (clips.TryGetValue("Block", out var blockClip))
            {
                var blockState = sm.AddState("Block");
                blockState.motion = blockClip;
                blockState.speed = 1f;

                var enter = idle.AddTransition(blockState);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Block");
                enter.duration = 0.18f;
                enter.hasExitTime = false;

                var exit = blockState.AddTransition(idle);
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Block");
                exit.duration = 0.20f;
                exit.hasExitTime = false;
            }
        }

        // Variant of AddTriggeredState that reuses an existing clip under a
        // new state-name + trigger, useful for tool swings that should look
        // like the sword-overhead PowerBash arc but be input-routable
        // independently.
        private static void AddTriggeredStateAlias(AnimatorController controller, AnimatorStateMachine sm,
            AnimatorState idle, System.Collections.Generic.Dictionary<string, AnimationClip> clips,
            string sourceClipKey, string stateName, string triggerName)
        {
            if (!clips.TryGetValue(sourceClipKey, out var clip)) return;

            var state = sm.AddState(stateName);
            state.motion = clip;
            state.speed = 1f;
            state.writeDefaultValues = false;

            var enter = idle.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            enter.duration = 0.20f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;
            enter.interruptionSource = TransitionInterruptionSource.Source;

            var exit = state.AddTransition(idle);
            exit.duration = 0.25f;
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.canTransitionToSelf = false;
        }

        // Generic helper: triggered one-shot state that auto-returns to Empty on exit.
        // Transition durations bumped from 0.06/0.18 -> 0.20/0.25 for smoother
        // ease-in/ease-out. canTransitionToSelf=false so spamming the trigger
        // can't restart the same swing on its first frame (input feels "queued"
        // when it actually drops the duplicate).
        private static void AddTriggeredState(AnimatorController controller, AnimatorStateMachine sm,
            AnimatorState idle, System.Collections.Generic.Dictionary<string, AnimationClip> clips,
            string clipKey, string triggerName, bool lookAtTrigger)
        {
            if (!clips.TryGetValue(clipKey, out var clip)) return;

            var state = sm.AddState(clipKey);
            state.motion = clip;
            state.speed = 1f;
            state.writeDefaultValues = false;

            var enter = idle.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            enter.duration = 0.20f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;
            enter.interruptionSource = TransitionInterruptionSource.Source;

            var exit = state.AddTransition(idle);
            exit.duration = 0.25f;
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.canTransitionToSelf = false;
        }

        // ----------------------------------------------- Layer 2: Reaction (override)

        private static void BuildReactionLayer(AnimatorController controller, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
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

            // Hit react bursts in then returns
            AddTriggeredState(controller, sm, idle, clips, "HitReact", "HitReact", lookAtTrigger: true);

            // Death: terminal state, no exit
            if (clips.TryGetValue("Death", out var deathClip))
            {
                var death = sm.AddState("Death");
                death.motion = deathClip;
                death.speed = 1f;
                death.writeDefaultValues = false;

                var enter = idle.AddTransition(death);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                enter.duration = 0.08f;
                enter.hasExitTime = false;
            }
        }

        // ----------------------------------------------- Helpers

        private static AnimationClip LoadFirstClipFromFbx(string fbxPath)
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), fbxPath))) return null;

            var sub = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
            foreach (var s in sub)
            {
                if (s is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            // Fallback: search all assets at that path (sometimes the clip is embedded as MainAsset).
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in allAssets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
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
