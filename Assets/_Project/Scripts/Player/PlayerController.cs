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

        [Header("Camera")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.3f, -3.5f);

        private CharacterController _cc;
        private PlayerInput _input;
        private Vector3 _velocity;
        private float _pitch;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInput>();
            if (GetComponent<PlayerVisualAnimator>() == null)
            {
                gameObject.AddComponent<PlayerVisualAnimator>();
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (Camera.main != null && cameraPivot != null)
            {
                Camera.main.transform.SetParent(cameraPivot, worldPositionStays: false);
                Camera.main.transform.localPosition = cameraOffset;
                Camera.main.transform.localRotation = Quaternion.identity;
            }
            Cursor.lockState = CursorLockMode.Locked;
        }

        // M1: owner-driven movement with NetworkTransform replicating to others.
        // M2 will introduce server-authoritative CmdMove for anti-cheat.
        private void Update()
        {
            if (!isLocalPlayer) return;

            var look = _input.LookAxis * mouseLookSpeed;
            transform.Rotate(0f, look.x, 0f);
            if (cameraPivot != null)
            {
                _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
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
