using System.Text;
using Steading.Player;
using UnityEngine;

namespace Steading.Building
{
    // Build-mode HUD overlay. PlayerInventory already draws its own resource
    // bar (top-left) via OnGUI, so this only renders the build bar (bottom)
    // when the local player is in build mode.
    [RequireComponent(typeof(BuildController))]
    public class BuildHud : MonoBehaviour
    {
        private BuildController _bc;
        private PlayerInventory _inventory;
        private GUIStyle _label;
        private GUIStyle _resBig;

        private void Awake()
        {
            _bc = GetComponent<BuildController>();
            _inventory = GetComponent<PlayerInventory>();
        }

        private void OnGUI()
        {
            if (_bc == null || !_bc.InBuildMode) return;
            EnsureStyles();
            DrawBuildBar();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
            };
            _resBig = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.95f, 0.85f) },
                alignment = TextAnchor.MiddleLeft,
            };
        }

        private void DrawBuildBar()
        {
            var label = string.IsNullOrEmpty(_bc.CurrentBuildableLabel) ? "?" : _bc.CurrentBuildableLabel;

            var bg = new Rect(0f, Screen.height - 88f, Screen.width, 70f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleRect = new Rect(0f, Screen.height - 84f, Screen.width, 26f);
            var costRect  = new Rect(0f, Screen.height - 60f, Screen.width, 22f);
            var helpRect  = new Rect(0f, Screen.height - 36f, Screen.width, 22f);

            var center = new GUIStyle(_resBig) { alignment = TextAnchor.MiddleCenter };
            var centerSmall = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };

            GUI.Label(titleRect, $"BUILD MODE — {label}", center);

            var costStr = _bc.CurrentCostString;
            if (!string.IsNullOrEmpty(costStr))
            {
                GUI.Label(costRect, $"Cost: {costStr}", centerSmall);
            }

            GUI.Label(helpRect,
                "Tab: cycle  •  R: rotate  •  LMB: place  •  RMB: delete  •  B: exit",
                centerSmall);
        }
    }
}
