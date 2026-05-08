using Steading.AI;
using UnityEngine;

namespace Steading.UI
{
    // Top-right HUD: countdown to next raid, or active-raid banner with
    // remaining-mob counter. No client RPCs needed — pulls SyncVar state
    // from RaidDirector.Instance every frame.
    public class RaidHud : MonoBehaviour
    {
        private GUIStyle _heading;
        private GUIStyle _label;

        private void OnGUI()
        {
            EnsureStyles();

            var rd = RaidDirector.Instance;
            if (rd == null) return;

            float w = 280f;
            float h = 64f;
            var rect = new Rect(Screen.width - w - 16f, 16f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (rd.ActiveRaid == RaidKind.None)
            {
                float remaining = Mathf.Max(0f, rd.NextRaidAt - (float)Mirror.NetworkTime.time);
                int m = Mathf.FloorToInt(remaining / 60f);
                int s = Mathf.FloorToInt(remaining - m * 60f);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 8f,  rect.width - 24f, 24f), "Next Raid", _heading);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 22f), $"{m:00}:{s:00}", _label);
            }
            else
            {
                var label = rd.ActiveRaid == RaidKind.HuntPlayer ? "RAID — HUNT PLAYER"
                          : rd.ActiveRaid == RaidKind.HuntStation ? "RAID — HUNT WORKBENCH"
                          : "RAID";
                GUI.color = new Color(0.6f, 0.10f, 0.10f, 0.55f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(rect.x + 12f, rect.y + 8f,  rect.width - 24f, 24f), label, _heading);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 22f),
                    $"Target: {rd.ActiveTargetName}   Remaining: {rd.ActiveRemaining}", _label);
            }
        }

        private void EnsureStyles()
        {
            if (_heading != null) return;
            _heading = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.95f, 0.85f) } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
        }
    }
}
