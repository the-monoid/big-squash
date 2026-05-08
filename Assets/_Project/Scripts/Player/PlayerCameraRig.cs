using Mirror;
using UnityEngine;
#if STEADING_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace Steading.Player
{
    // Local-player only. Spawns a Cinemachine third-person camera rig that
    // follows the player's spine bone instead of reparenting Camera.main onto
    // a fake CameraPivot transform. Replaces the procedural camera-reparenting
    // path in PlayerController.OnStartLocalPlayer.
    //
    // The actual CinemachineCamera prefab is built by
    // SteadingPlayerCameraSetup (editor menu). At runtime this component
    // instantiates that prefab and binds Follow/LookAt to the right bones.
    public class PlayerCameraRig : NetworkBehaviour
    {
        [Tooltip("Prefab containing the CinemachineCamera + ThirdPersonFollow. Built by Steading > Animator: Build Player Camera Rig menu.")]
        [SerializeField] private GameObject cameraRigPrefab;

        [Tooltip("Bone name to use for camera target. Defaults to mixamorig:Spine2 for the X Bot rig.")]
        [SerializeField] private string targetBoneName = "mixamorig:Spine2";

        [Tooltip("If non-null, falls back to this transform when targetBoneName isn't found.")]
        [SerializeField] private Transform fallbackTarget;

        private GameObject _spawnedRig;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            if (cameraRigPrefab == null)
            {
                Debug.LogWarning("[Steading] PlayerCameraRig: cameraRigPrefab is null. Run 'Steading/Animator: Build Player Camera Rig' or assign manually.");
                return;
            }

            _spawnedRig = Instantiate(cameraRigPrefab);
            _spawnedRig.name = "Player Camera Rig (Local)";

            var target = FindBone(transform, targetBoneName) ?? fallbackTarget ?? transform;
            BindRig(_spawnedRig, target);

            // Lock + hide the cursor for gameplay.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            if (_spawnedRig != null) Destroy(_spawnedRig);
        }

        // ------------------------------------------------- Cinemachine binding
        // Wrapped in #if so the project compiles even before the user opens Unity
        // and Cinemachine resolves. Once the package is in, STEADING_CINEMACHINE
        // is defined via asmdef versionDefines.

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
