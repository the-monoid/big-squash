using Steading.Building;
using Steading.Combat;
using Steading.Player;
using UnityEngine;

namespace Steading.UI
{
    // Local-player overlay: detect nearby CraftingStation, prompt "Press E to
    // craft", open a panel listing every WeaponLibrary entry's recipe with
    // afford/unlock state, and route Craft clicks to CraftingStation.CmdCraft.
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlayerAttack))]
    public class CraftingHud : MonoBehaviour
    {
        [SerializeField] private float searchRadius = 4f;
        [SerializeField] private KeyCode openKey = KeyCode.E;

        private PlayerInventory _inventory;
        private PlayerAttack _attack;
        private CraftingStation _activeStation;
        private bool _open;
        private GUIStyle _label;
        private GUIStyle _row;
        private GUIStyle _heading;
        private Vector2 _scroll;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _attack = GetComponent<PlayerAttack>();
        }

        private void Update()
        {
            if (!IsLocalPlayer()) return;

            _activeStation = FindNearestStation();

            if (Input.GetKeyDown(openKey))
            {
                if (_activeStation != null)
                {
                    _open = !_open;
                    Cursor.visible = _open;
                    Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
                }
                else if (_open)
                {
                    Close();
                }
            }
            if (_open && Input.GetKeyDown(KeyCode.Escape)) Close();
            if (_open && _activeStation == null) Close();
        }

        private bool IsLocalPlayer()
        {
            // PlayerAttack is a NetworkBehaviour; use its isLocalPlayer flag
            // to avoid this HUD rendering for remote clients' player objects.
            return _attack != null && _attack.isLocalPlayer;
        }

        private void Close()
        {
            _open = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private CraftingStation FindNearestStation()
        {
            CraftingStation best = null;
            float bestDistSqr = searchRadius * searchRadius;
            foreach (var s in CraftingStation.ActiveStations)
            {
                if (s == null) continue;
                var d = (s.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistSqr) { bestDistSqr = d; best = s; }
            }
            return best;
        }

        private void OnGUI()
        {
            if (!IsLocalPlayer()) return;
            EnsureStyles();

            // Prompt
            if (_activeStation != null && !_open)
            {
                var promptRect = new Rect(Screen.width / 2f - 140f, Screen.height - 200f, 280f, 30f);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(promptRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(promptRect, $"Press {openKey} to use Workbench", _heading);
            }

            if (!_open || _activeStation == null) return;
            DrawCraftingPanel();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            _row = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 6, 6) };
            _heading = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.95f, 0.85f) } };
        }

        private void DrawCraftingPanel()
        {
            var lib = WeaponLibrary.Instance;
            if (lib == null) return;

            var w = 460f;
            var h = 480f;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 26f), "Workbench — Craft Weapons", _heading);

            var listRect = new Rect(rect.x + 14f, rect.y + 50f, rect.width - 28f, rect.height - 100f);
            var contentRect = new Rect(0f, 0f, listRect.width - 18f, lib.weapons.Count * 70f);
            _scroll = GUI.BeginScrollView(listRect, _scroll, contentRect);

            for (int i = 0; i < lib.weapons.Count; i++)
            {
                var def = lib.weapons[i];
                if (def == null) continue;

                var rowRect = new Rect(0f, i * 70f, contentRect.width, 64f);
                bool unlocked = _attack.IsUnlocked(i);
                bool affordable = _inventory.CanAfford(def.cost);
                bool isStarter = def.starter;

                // Background
                GUI.color = unlocked ? new Color(0.20f, 0.40f, 0.20f, 0.55f) :
                            affordable ? new Color(0.20f, 0.30f, 0.45f, 0.55f) :
                                         new Color(0.30f, 0.20f, 0.20f, 0.55f);
                GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(rowRect.x + 12f, rowRect.y + 4f, 240f, 20f),
                    $"{def.displayName} ({def.tier})", _label);
                GUI.Label(new Rect(rowRect.x + 12f, rowRect.y + 24f, 300f, 20f),
                    $"Light {def.damageLight} / Heavy {def.damageHeavy}   CD {def.cooldownLight:F2}s", _label);
                GUI.Label(new Rect(rowRect.x + 12f, rowRect.y + 44f, 300f, 20f),
                    BuildCostString(def), _label);

                // Action button
                var btnRect = new Rect(rowRect.xMax - 110f, rowRect.y + 18f, 100f, 28f);
                if (isStarter || unlocked)
                {
                    GUI.Label(btnRect, unlocked ? "Unlocked" : "Starter", _heading);
                }
                else if (!affordable)
                {
                    GUI.Label(btnRect, "Need more", _label);
                }
                else
                {
                    if (GUI.Button(btnRect, "Craft", _row))
                    {
                        _activeStation.CmdCraft(i);
                    }
                }
            }

            GUI.EndScrollView();

            var closeRect = new Rect(rect.x + (rect.width - 120f) / 2f, rect.yMax - 40f, 120f, 28f);
            if (GUI.Button(closeRect, "Close (Esc)", _row)) Close();
        }

        private string BuildCostString(WeaponDef def)
        {
            if (def.cost == null || def.cost.Length == 0) return "Free";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < def.cost.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(def.cost[i].amount).Append(' ').Append(def.cost[i].kind);
            }
            return sb.ToString();
        }
    }
}
