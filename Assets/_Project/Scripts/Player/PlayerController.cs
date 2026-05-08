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
        private Vector3 _velocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInput>();
            // PlayerVisualAnimator (procedural blob rig) is no longer auto-added;
            // PlayerAnimatorBridge drives the imported VikingHero Animator instead.
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            // Camera setup is handled by PlayerCameraRig (Cinemachine third-person rig).
            // PlayerCameraRig.OnStartLocalPlayer locks the cursor itself.
        }

        // M1: owner-driven movement with NetworkTransform replicating to others.
        // M2 will introduce server-authoritative CmdMove for anti-cheat.
        private void Update()
        {
            if (!isLocalPlayer) return;

            // Yaw the player with mouse-X. Pitch is handled by Cinemachine's
            // POV/RotationComposer on the camera, not by us — the player body
            // rotates around Y only.
            var look = _input.LookAxis * mouseLookSpeed;
            transform.Rotate(0f, look.x, 0f);

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
