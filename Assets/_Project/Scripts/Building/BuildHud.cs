using UnityEngine;

namespace Steading.Building
{
    // Minimal OnGUI overlay shown while the local player is in build mode.
    // Displays current buildable name + control reminders. Replaced in M6
    // by a proper UI Toolkit panel.
    [RequireComponent(typeof(BuildController))]
    public class BuildHud : MonoBehaviour
    {
        private BuildController _bc;
        private GUIStyle _style;

        private void Awake() => _bc = GetComponent<BuildController>();

        private void OnGUI()
        {
            if (_bc == null || !_bc.InBuildMode) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
            };

            var label = string.IsNullOrEmpty(_bc.CurrentBuildableLabel) ? "?" : _bc.CurrentBuildableLabel;
            var rect = new Rect(0f, Screen.height - 80f, Screen.width, 30f);
            var bg = new Rect(rect.x, rect.y, rect.width, 60f);

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(rect, $"BUILD MODE — Selected: {label}", _style);
            GUI.Label(new Rect(0f, Screen.height - 50f, Screen.width, 30f),
                "Tab: cycle  •  R: rotate  •  Left-click: place  •  Right-click: delete  •  B: exit", _style);
        }
    }
}
