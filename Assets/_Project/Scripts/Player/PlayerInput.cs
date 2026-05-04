using UnityEngine;

namespace Steading.Player
{
    // Thin wrapper over the new Input System. M1 keeps it in legacy mode so the
    // project compiles without the Input System package; swap for InputAction
    // bindings once the package is installed.
    public class PlayerInput : MonoBehaviour
    {
        public Vector2 MoveAxis { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }

        private void Update()
        {
            MoveAxis = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            SprintHeld = Input.GetKey(KeyCode.LeftShift);
            JumpPressed = Input.GetKeyDown(KeyCode.Space);
        }
    }
}
