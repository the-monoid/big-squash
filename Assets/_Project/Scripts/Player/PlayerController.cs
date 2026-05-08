using Mirror;
using UnityEngine;

namespace Steading.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float mouseLookSpeed = 2.2f;

        [Header("Free-form locomotion")]
        [Tooltip("When true, A/D moves perpendicular to the camera's facing (not the body's), and the body smoothly rotates to face the actual movement direction. Reads as full free-look strafing instead of foot-skating in place.")]
        [SerializeField] private bool freeFormMovement = true;
        [Tooltip("Degrees per second the body rotates to face the movement direction while moving.")]
        [SerializeField] private float bodyTurnSpeed = 720f;

        private CharacterController _cc;
        private PlayerInput _input;
        private PlayerCameraRig _cameraRig;
        private Vector3 _velocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInput>();
            _cameraRig = GetComponent<PlayerCameraRig>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            // Cursor lock is owned by PlayerCameraRig.
        }

        // Owner-driven movement. NetworkTransform replicates position+rotation
        // to other clients.
        private void Update()
        {
            if (!isLocalPlayer) return;

            var move = _input.MoveAxis;
            var speed = _input.SprintHeld ? runSpeed : walkSpeed;

            Vector3 motion;
            if (freeFormMovement && _cameraRig != null)
            {
                // CAMERA-RELATIVE movement. WASD direction interprets in
                // camera flat-space — W = into the screen, A/D = perpendicular
                // to camera, S = back. The body smoothly rotates to face the
                // actual world-space movement vector instead of staying locked
                // to camera yaw, so strafing reads as a real turn instead of
                // foot-skating.
                float camYawDeg = _cameraRig.YawDeg;
                var camRotY = Quaternion.Euler(0f, camYawDeg, 0f);
                var dir = camRotY * new Vector3(move.x, 0f, move.y);
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                motion = dir * speed;

                if (motion.sqrMagnitude > 0.01f)
                {
                    var targetRot = Quaternion.LookRotation(motion.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, targetRot, bodyTurnSpeed * Time.deltaTime);
                }
                else
                {
                    // Idle facing — point the body where the camera is looking.
                    var targetRot = Quaternion.Euler(0f, camYawDeg, 0f);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, targetRot, bodyTurnSpeed * 0.5f * Time.deltaTime);
                }
            }
            else
            {
                // Legacy body-relative movement (Valheim-tank). Yaw with mouse-X
                // either via the camera rig (preferred) or directly if no rig.
                if (_cameraRig != null)
                {
                    transform.rotation = Quaternion.Euler(0f, _cameraRig.YawDeg, 0f);
                }
                else
                {
                    var look = _input.LookAxis * mouseLookSpeed;
                    transform.Rotate(0f, look.x, 0f);
                }
                motion = (transform.right * move.x + transform.forward * move.y) * speed;
            }

            if (_cc.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
            if (_input.JumpPressed && _cc.isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _velocity.y += gravity * Time.deltaTime;

            _cc.Move((motion + Vector3.up * _velocity.y) * Time.deltaTime);
        }
    }
}
