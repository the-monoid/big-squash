using UnityEditor;
using UnityEngine;
#if STEADING_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace Steading.EditorTools
{
    // Builds the Cinemachine third-person camera prefab. Idempotent.
    //
    // Output prefab at Assets/_Project/Prefabs/PlayerCameraRig.prefab containing:
    //   - GameObject "PlayerCameraRig"
    //     - "Brain" GameObject with CinemachineBrain (lives on the actual main camera)
    //     - "ThirdPersonCam" with CinemachineCamera + CinemachineThirdPersonFollow
    //
    // PlayerCameraRig.cs (runtime) instantiates this on the local player's spawn
    // and binds Follow/LookAt to the imported FBX's spine bone.
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

            var root = new GameObject("PlayerCameraRig");

            // Cinemachine Brain — receives camera updates and feeds them to the
            // active Camera. Brain expects to live on the actual rendering camera,
            // so we add it to a child labeled MainCamera.
            var brainGo = new GameObject("Main Camera") { tag = "MainCamera" };
            brainGo.transform.SetParent(root.transform, worldPositionStays: false);
            var cam = brainGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            brainGo.AddComponent<AudioListener>();
            brainGo.AddComponent<CinemachineBrain>();

            // The CinemachineCamera object — driven by Follow/LookAt.
            var camGo = new GameObject("ThirdPersonCam");
            camGo.transform.SetParent(root.transform, worldPositionStays: false);

            var cmCam = camGo.AddComponent<CinemachineCamera>();
            cmCam.Lens.FieldOfView = 52f;
            cmCam.Priority.Value = 10;

            // Third-person over-the-shoulder body. Cinemachine 3.x: components are
            // CinemachineThirdPersonFollow + (optional) CinemachineRotationComposer.
            var follow = camGo.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0.55f, 0.45f, 0f);
            follow.VerticalArmLength = 0.4f;
            follow.CameraDistance = 3.6f;
            follow.CameraSide = 1f;          // right shoulder
            follow.CameraCollisionFilter = ~0;
            follow.IgnoreTag = "Player";
            follow.AvoidObstacles.Enabled = true;
            follow.AvoidObstacles.CameraRadius = 0.18f;
            follow.AvoidObstacles.SmoothingTime = 0.06f;
            follow.AvoidObstacles.Damping = 0.5f;

            var rotation = camGo.AddComponent<CinemachineRotationComposer>();
            rotation.TargetOffset = new Vector3(0f, 0.4f, 0f);
            rotation.Damping = new Vector2(0.18f, 0.22f);

            // Save as prefab.
            EnsureFolder("Assets/_Project/Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            EditorUtility.DisplayDialog(
                "Player Camera Rig",
                "Built PlayerCameraRig.prefab. Re-run M1Setup to wire it into Player.prefab.",
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
