using Mirror;
using UnityEngine;

namespace Steading.Player
{
    // Hand-rolled third-person camera. Replaces Cinemachine entirely — fewer
    // moving parts, deterministic across Cinemachine 3.x patch releases, and
    // we know exactly what it does at every frame.
    //
    // Behavior:
    //   * Mouse-X yaws the player body (PlayerController also does this; we
    //     don't double-rotate — only the camera reads mouse-X for its own
    //     yaw smoothing here, and PlayerController already yaws the body).
    //   * Mouse-Y pitches the camera up/down, clamped.
    //   * Camera position = orbit around a Chest-bone anchor at (offsetSide,
    //     offsetUp, -offsetBack). Smooth-damped to avoid jitter.
    //   * Spherecast from anchor toward desired position; if anything blocks,
    //     pull camera in.
    //
    // Drives Camera.main directly — no Brain, no CmCamera, no second camera.
    public class PlayerCameraRig : NetworkBehaviour
    {
        [Header("Anchor")]
        [Tooltip("Bone name fallback if Animator.GetBoneTransform misses (rare).")]
        [SerializeField] private string fallbackBoneName = "mixamorig:Spine2";
        [SerializeField] private float anchorUpOffset = 0.35f;

        [Header("Orbit")]
        [SerializeField] private float distance = 3.6f;
        [SerializeField] private float shoulderSide = 0.45f;
        [SerializeField] private float verticalOffset = 0.3f;
        [SerializeField] private float pitchMin = -35f;
        [SerializeField] private float pitchMax = 70f;
        [SerializeField] private float lookSpeed = 2.4f;

        [Header("Smoothing")]
        [SerializeField] private float positionSmooth = 0.06f;
        [SerializeField] private float rotationSmooth = 0.04f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.20f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [Tooltip("Don't push the camera closer than this fraction of distance when colliding.")]
        [SerializeField] private float minCollisionDistance = 0.4f;

        private Transform _anchor;
        private Camera _cam;
        private float _yaw;
        private float _pitch;
        private Vector3 _smoothPos;

        public float YawDeg => _yaw;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            _cam = Camera.main;
            if (_cam == null)
            {
                Debug.LogError("[Steading] PlayerCameraRig: no Main Camera in scene.");
                enabled = false;
                return;
            }

            _anchor = ResolveAnchor();
            _yaw = transform.eulerAngles.y;
            _pitch = 12f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"[Steading] PlayerCameraRig active — Camera='{_cam.name}', anchor='{_anchor.name}'.");
        }

        private Transform ResolveAnchor()
        {
            // 1) Humanoid bone via Animator
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform bone = animator.GetBoneTransform(HumanBodyBones.Chest)
                              ?? animator.GetBoneTransform(HumanBodyBones.Spine)
                              ?? animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone != null) return MakeOffsetAnchor(bone, new Vector3(0f, anchorUpOffset, 0f));
            }

            // 2) Literal bone name
            var literal = FindBone(transform, fallbackBoneName);
            if (literal != null) return MakeOffsetAnchor(literal, new Vector3(0f, anchorUpOffset, 0f));

            // 3) Synthesized chest-height anchor on player root
            return MakeOffsetAnchor(transform, new Vector3(0f, 1.55f, 0f));
        }

        private void LateUpdate()
        {
            if (!isLocalPlayer || _cam == null || _anchor == null) return;

            // Mouse pitch (yaw is owned by PlayerController so the body turns
            // with the camera). We track our own yaw in sync with the player.
            var mx = Input.GetAxis("Mouse X") * lookSpeed;
            var my = Input.GetAxis("Mouse Y") * lookSpeed;
            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch - my, pitchMin, pitchMax);

            // Where the camera WANTS to be in world space.
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var anchorWorld = _anchor.position;
            var idealOffset = rot * new Vector3(shoulderSide, verticalOffset, -distance);
            var idealPos = anchorWorld + idealOffset;

            // Collision: pull in if a wall is in the way.
            var dirFromAnchor = (idealPos - anchorWorld);
            var d = dirFromAnchor.magnitude;
            if (d > 0.001f &&
                Physics.SphereCast(anchorWorld, collisionRadius, dirFromAnchor.normalized, out var hit, d, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Collider>()?.transform != transform)
                {
                    var clamped = Mathf.Max(hit.distance - collisionRadius * 0.5f, distance * minCollisionDistance);
                    idealPos = anchorWorld + dirFromAnchor.normalized * clamped;
                }
            }

            _smoothPos = Vector3.SmoothDamp(_cam.transform.position, idealPos, ref _smoothVel, positionSmooth);
            _cam.transform.position = _smoothPos;

            // Look toward the anchor (slightly above).
            var lookTarget = anchorWorld + Vector3.up * 0.05f;
            var targetRot = Quaternion.LookRotation(lookTarget - _cam.transform.position, Vector3.up);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, targetRot, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, rotationSmooth)));
        }
        private Vector3 _smoothVel;

        private static Transform MakeOffsetAnchor(Transform parent, Vector3 localOffset)
        {
            var go = new GameObject("CameraAnchor");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localOffset;
            return go.transform;
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
