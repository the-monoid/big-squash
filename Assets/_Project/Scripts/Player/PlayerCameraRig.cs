using Mirror;
using UnityEngine;
#if STEADING_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace Steading.Player
{
    // Local-player only. Spawns a CinemachineCamera (built by
    // SteadingPlayerCameraRigBuilder) and ensures the scene's Main Camera has a
    // CinemachineBrain to drive it.
    //
    // Why this shape: the rig prefab is intentionally JUST a CinemachineCamera —
    // it does not contain its own Camera or Brain. M1Setup created the scene's
    // Main Camera; we attach a Brain to that one. Result: exactly ONE Camera
    // in the scene, no tagging fights, no "POV inside the character" surprise.
    public class PlayerCameraRig : NetworkBehaviour
    {
        [Tooltip("Prefab containing the CinemachineCamera + ThirdPersonFollow. Built by Steading > Animator: Build Player Camera Rig.")]
        [SerializeField] private GameObject cameraRigPrefab;

        [Tooltip("Bone name to use for camera target. Defaults to mixamorig:Spine2 for the X Bot rig.")]
        [SerializeField] private string targetBoneName = "mixamorig:Spine2";

        [Tooltip("If non-null, falls back to this transform when targetBoneName isn't found.")]
        [SerializeField] private Transform fallbackTarget;

        [Tooltip("Vertical offset added to the bone target so the camera lines up with the player's head (the Mixamo spine is below the head by ~0.4m).")]
        [SerializeField] private float targetUpOffset = 0.35f;

        private GameObject _spawnedRig;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            if (cameraRigPrefab == null)
            {
                Debug.LogWarning("[Steading] PlayerCameraRig: cameraRigPrefab is null. Run 'Steading/Animator: Build Player Camera Rig' or assign manually.");
                return;
            }

            EnsureBrainOnSceneCamera();

            _spawnedRig = Instantiate(cameraRigPrefab);
            _spawnedRig.name = "PlayerThirdPersonCam (Local)";

            var target = ResolveCameraTarget();
            BindRig(_spawnedRig, target);

            Debug.Log($"[Steading] PlayerCameraRig bound to '{target.name}' at world {target.position}.");

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Best-effort camera anchor resolution. Tries Humanoid avatar first
        // (works regardless of source naming, e.g. "mixamorig:Spine2" vs just
        // "Spine2"), then a literal bone-name search, then fallbackTarget,
        // then a synthesized chest-height anchor on the player root.
        private Transform ResolveCameraTarget()
        {
            // Attempt 1: Humanoid avatar's Chest/Spine bone via Animator.
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform bone = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
                if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone != null)
                {
                    return MakeOffsetAnchor(bone, new Vector3(0f, targetUpOffset, 0f));
                }
            }

            // Attempt 2: literal bone name (Mixamo "mixamorig:Spine2" etc.).
            var named = FindBone(transform, targetBoneName);
            if (named != null) return MakeOffsetAnchor(named, new Vector3(0f, targetUpOffset, 0f));

            // Attempt 3: serialized fallback.
            if (fallbackTarget != null) return fallbackTarget;

            // Attempt 4: synthesize a chest-height anchor on the player root.
            return MakeOffsetAnchor(transform, new Vector3(0f, 1.55f, 0f));
        }

        private static Transform MakeOffsetAnchor(Transform parent, Vector3 localOffset)
        {
            var go = new GameObject("CameraTarget");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localOffset;
            return go.transform;
        }

        private void OnDestroy()
        {
            if (_spawnedRig != null) Destroy(_spawnedRig);
        }

        // ------------------------------------------------- Cinemachine plumbing

        private static void EnsureBrainOnSceneCamera()
        {
#if STEADING_CINEMACHINE
            var cam = Camera.main;
            if (cam == null) return;
            if (cam.GetComponent<CinemachineBrain>() == null)
            {
                cam.gameObject.AddComponent<CinemachineBrain>();
            }
#endif
        }

        private static void BindRig(GameObject rig, Transform target)
        {
#if STEADING_CINEMACHINE
            var cam = rig.GetComponentInChildren<CinemachineCamera>();
            if (cam != null)
            {
                cam.Follow = target;
                cam.LookAt = target;
            }
#else
            // Fallback for pre-Cinemachine state: just place the rig at the target.
            rig.transform.SetParent(target, worldPositionStays: false);
#endif
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindBone(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
