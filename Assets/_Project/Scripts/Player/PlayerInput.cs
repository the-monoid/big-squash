using UnityEngine;

namespace Steading.Player
{
    // Thin wrapper over Unity's legacy Input axes. Phase 2 added Crouch/Block
    // for the New-World shield-rush + charged-power-bash combat. Will swap to
    // the new Input System once we have an action asset defined.
    public class PlayerInput : MonoBehaviour
    {
        public Vector2 MoveAxis { get; private set; }
        public Vector2 LookAxis { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool BlockHeld { get; private set; }

        private void Update()
        {
            MoveAxis = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            LookAxis = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            SprintHeld  = Input.GetKey(KeyCode.LeftShift);
            JumpPressed = Input.GetKeyDown(KeyCode.Space);
            CrouchHeld  = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            BlockHeld   = Input.GetMouseButton(1);
        }
    }
}
