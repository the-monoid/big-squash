using UnityEditor;
using UnityEngine;
#if STEADING_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace Steading.EditorTools
{
    // Builds a single-component CinemachineCamera prefab. Idempotent.
    //
    // Output: Assets/_Project/Prefabs/PlayerCameraRig.prefab
    //   GameObject "PlayerThirdPersonCam"
    //     - CinemachineCamera
    //     - CinemachineThirdPersonFollow
    //     - CinemachineRotationComposer
    //
    // Critically: the prefab does NOT contain its own Camera + CinemachineBrain.
    // Instead, the runtime PlayerCameraRig.cs adds a Brain to the scene's
    // existing Main Camera (set up by M1Setup). One Camera, one Brain — no
    // tagging fights, no "POV inside the character" surprises.
    public static class PlayerCameraRigBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/PlayerCameraRig.prefab";

        [MenuItem("Steading/Animator: Build Player Camera Rig")]
        public static void Build()
        {
#if !STEADING_CINEMACHINE
            EditorUtility.DisplayDialog(
                "Player Camera Rig",
                "Cinemachine package is not installed. Open Window > Package Manager, " +
                "verify com.unity.cinemachine 3.x, then re-run this menu.",
                "OK");
            return;
#else
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) AssetDatabase.DeleteAsset(PrefabPath);

            var camGo = new GameObject("PlayerThirdPersonCam");

            var cmCam = camGo.AddComponent<CinemachineCamera>();
            cmCam.Lens.FieldOfView = 52f;
            cmCam.Priority.Value = 10;

            var follow = camGo.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset    = new Vector3(0.55f, 0.45f, 0f);
            follow.VerticalArmLength = 0.4f;
            follow.CameraDistance    = 3.6f;
            follow.CameraSide        = 1f;      // right shoulder
            follow.Damping           = new Vector3(0.10f, 0.12f, 0.10f);

            var obstacles = follow.AvoidObstacles;
            obstacles.Enabled              = true;
            obstacles.CameraRadius         = 0.18f;
            obstacles.CollisionFilter      = ~0;
            obstacles.IgnoreTag            = "Player";
            obstacles.DampingIntoCollision = 0.06f;
            obstacles.DampingFromCollision = 0.30f;
            follow.AvoidObstacles = obstacles;

            // Default RotationComposer keeps the camera aimed at LookAt; field
            // names vary across 3.1.x patches, so we just attach with defaults
            // and let the user tune in the Inspector if desired.
            camGo.AddComponent<CinemachineRotationComposer>();

            EnsureFolder("Assets/_Project/Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(camGo, PrefabPath);
            Object.DestroyImmediate(camGo);

            EditorUtility.DisplayDialog(
                "Player Camera Rig",
                "Built PlayerCameraRig.prefab (CinemachineCamera only — no extra Camera/Brain). " +
                "Re-run M1Setup to wire it into Player.prefab.",
                "OK");
#endif
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
