using System.Text;
using UnityEngine;

namespace Steading.Building
{
    // Minimal OnGUI overlay shown while the local player is in build mode.
    // - Resource bar (always visible to the local player, even outside build mode)
    // - Selected buildable + cost + control reminders (build mode only)
    [RequireComponent(typeof(BuildController))]
    public class BuildHud : MonoBehaviour
    {
        private BuildController _bc;
        private ResourceWallet _wallet;
        private GUIStyle _label;
        private GUIStyle _resBig;
        private readonly StringBuilder _sb = new StringBuilder();

        private void Awake()
        {
            _bc = GetComponent<BuildController>();
            _wallet = GetComponent<ResourceWallet>();
        }

        private void OnGUI()
        {
            if (_bc == null) return;
            EnsureStyles();

            DrawResourceBar();

            if (_bc.InBuildMode) DrawBuildBar();
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

        private void DrawResourceBar()
        {
            if (_wallet == null) return;

            // Top-left strip showing resource totals.
            var bgRect = new Rect(8f, 8f, 220f, 30f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            _sb.Length = 0;
            _sb.Append("🪵 ").Append(_wallet.Wood)
               .Append("   🪨 ").Append(_wallet.Stone)
               .Append("   ⚒ ").Append(_wallet.Iron)
               .Append("   🐺 ").Append(_wallet.Hide);

            GUI.Label(new Rect(bgRect.x + 8f, bgRect.y, bgRect.width - 8f, bgRect.height), _sb.ToString(), _resBig);
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
