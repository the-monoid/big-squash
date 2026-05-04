using UnityEngine;

namespace Steading.Player
{
    // Thin wrapper over Unity's legacy Input axes for M1. M2 swaps to the new
    // Input System once we have action assets defined.
    public class PlayerInput : MonoBehaviour
    {
        public Vector2 MoveAxis { get; private set; }
        public Vector2 LookAxis { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }

        private void Update()
        {
            MoveAxis = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            LookAxis = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            SprintHeld = Input.GetKey(KeyCode.LeftShift);
            JumpPressed = Input.GetKeyDown(KeyCode.Space);
        }
    }
}
