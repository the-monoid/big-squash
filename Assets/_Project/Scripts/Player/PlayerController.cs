using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Steading.Player
{
#if MIRROR
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _cc;
        private Vector3 _velocity;
        private PlayerInput _input;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInput>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            // Bind camera to local player here in M1+ work.
        }

        // Server-authoritative movement: client sends inputs, server moves.
        // For M1 we run a simple shared CharacterController on the owner and let
        // NetworkTransform replicate position. M2 will introduce CmdMove for
        // anti-cheat hardening.
        private void Update()
        {
            if (!isLocalPlayer) return;

            var move = _input.MoveAxis;
            var sprint = _input.SprintHeld;
            var speed = sprint ? runSpeed : walkSpeed;

            var motion = transform.right * move.x + transform.forward * move.y;
            motion *= speed;

            if (_cc.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
            if (_input.JumpPressed && _cc.isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _velocity.y += gravity * Time.deltaTime;

            _cc.Move((motion + new Vector3(0, _velocity.y, 0)) * Time.deltaTime);
        }
    }
#else
    public class PlayerController : MonoBehaviour { }
#endif
}
