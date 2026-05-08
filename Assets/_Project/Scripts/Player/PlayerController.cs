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
            // Cursor lock + Cinemachine setup are owned by PlayerCameraRig now.
        }

        // Owner-driven movement. NetworkTransform replicates position+rotation
        // to other clients. M2 will introduce server-authoritative CmdMove for
        // anti-cheat hardening.
        private void Update()
        {
            if (!isLocalPlayer) return;

            // Yaw: PlayerCameraRig owns mouse-X; the body follows it. Falls back
            // to direct mouse-X handling if no rig is present (server build,
            // remote client, etc.).
            if (_cameraRig != null)
            {
                transform.rotation = Quaternion.Euler(0f, _cameraRig.YawDeg, 0f);
            }
            else
            {
                var look = _input.LookAxis * mouseLookSpeed;
                transform.Rotate(0f, look.x, 0f);
            }

            var move = _input.MoveAxis;
            var speed = _input.SprintHeld ? runSpeed : walkSpeed;
            var motion = (transform.right * move.x + transform.forward * move.y) * speed;

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
