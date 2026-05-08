using UnityEngine;

namespace Steading.UI
{
    // Drives the Main Menu's camera + lighting + atmosphere so the scene reads
    // as a painterly portrait of the character instead of a flat grey backdrop.
    //
    // Behavior:
    //   * Camera slowly orbits the character at fixed radius/height.
    //   * Warm directional key light + cool rim light auto-positioned for a
    //     three-point setup.
    //   * Skybox swapped to Steading/PainterlySky if the asset is in Resources
    //     or already assigned to RenderSettings.
    //   * Fog enabled with the painterly horizon color.
    //
    // Drop on a GameObject in MainMenu.unity. MainMenuSetup auto-attaches it.
    public class MainMenuPresenter : MonoBehaviour
    {
        [Header("Camera (does NOT orbit — codex's main menu lays out the panel on the LEFT and frames the character on the RIGHT, so we preserve that camera position and only do a tiny breathing micro-sway)")]
        [SerializeField] private Transform target;            // character preview root
        [SerializeField] private bool overrideCameraPosition = false;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0.55f, 1.45f, -4.45f);
        [SerializeField] private Vector3 cameraLookAt = new Vector3(0.78f, 1.04f, 0.05f);
        [SerializeField] private float swayAmplitudeY = 0.012f;
        [SerializeField] private float swayAmplitudeX = 0.018f;
        [SerializeField] private float swayRate = 0.18f;
        [SerializeField] private float fov = 48f;             // matches codex's original

        [Header("Lighting")]
        [SerializeField] private Color keyLightColor  = new Color(1.20f, 1.05f, 0.78f);
        [SerializeField] private float keyLightIntensity = 1.55f;
        [SerializeField] private Color rimLightColor  = new Color(0.50f, 0.65f, 1.00f);
        [SerializeField] private float rimLightIntensity = 0.85f;
        [SerializeField] private Color fillLightColor = new Color(0.80f, 0.78f, 0.70f);
        [SerializeField] private float fillLightIntensity = 0.45f;

        [Header("Atmosphere")]
        [SerializeField] private Color fogColor = new Color(0.62f, 0.58f, 0.52f);
        [SerializeField] private float fogDensity = 0.018f;

        private Camera _cam;
        private Light _keyLight;
        private Light _rimLight;
        private Light _fillLight;
        private Vector3 _basePosition;
        private Vector3 _baseLookAt;

        private void Awake()
        {
            ResolveCamera();
            ResolveTarget();
            BuildLights();
            ApplyAtmosphere();
        }

        private void OnEnable()
        {
            CaptureBaseCameraPose();
        }

        private void Update()
        {
            ApplyMicroSway();
        }

        // ----------------------------------------------------------------- setup

        private void ResolveCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 200f;
            // FOV: only override if we're also overriding the position. Otherwise
            // accept whatever MainMenuSetup configured so the menu panel + character
            // framing stays correct.
            if (overrideCameraPosition) _cam.fieldOfView = fov;
        }

        private void ResolveTarget()
        {
            if (target != null) return;

            // Look for a preview character in the scene (CharacterCustomization or
            // PlayerVisualAnimator are both good anchors). If nothing found, use
            // world origin.
            var animator = FindFirstObjectByType<Steading.Player.PlayerAnimatorBridge>();
            if (animator != null) target = animator.transform;
        }

        private void BuildLights()
        {
            _keyLight  = MakeOrFindDirectional("Menu Key Light",  keyLightColor,  keyLightIntensity,  Quaternion.Euler(38f, -38f, 0f), shadows: true);
            _rimLight  = MakeOrFindDirectional("Menu Rim Light",  rimLightColor,  rimLightIntensity,  Quaternion.Euler(22f, 152f, 0f), shadows: false);
            _fillLight = MakeOrFindDirectional("Menu Fill Light", fillLightColor, fillLightIntensity, Quaternion.Euler(-12f, 36f, 0f), shadows: false);
        }

        private Light MakeOrFindDirectional(string name, Color color, float intensity, Quaternion rot, bool shadows)
        {
            var existing = transform.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.rotation = rot;

            var l = go.GetComponent<Light>() ?? go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            return l;
        }

        private void ApplyAtmosphere()
        {
            // Try to load a painterly sky material if one is in Resources or already
            // present in RenderSettings — don't overwrite a custom sky the user set.
            var sky = RenderSettings.skybox;
            if (sky == null || sky.shader == null || sky.shader.name == "Skybox/Procedural" || sky.shader.name == "Skybox/Default")
            {
                var painterly = Resources.Load<Material>("PainterlySky");
                if (painterly != null) RenderSettings.skybox = painterly;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor    = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.55f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.14f);
        }

        // ----------------------------------------------------------------- per-frame

        // Either accept whatever pose the camera came in with (set by MainMenuSetup
        // to frame the character on the right side past the menu panel) or apply
        // our serialized override. The presenter never moves the camera further
        // than the small breathing sway in ApplyMicroSway.
        private void CaptureBaseCameraPose()
        {
            if (_cam == null) return;

            if (overrideCameraPosition)
            {
                _cam.transform.position = cameraPosition;
                _cam.transform.LookAt(cameraLookAt);
            }

            _basePosition = _cam.transform.position;
            _baseLookAt = _cam.transform.position + _cam.transform.forward * 5f;
        }

        // Tiny up/right sway around the base pose so the framing breathes without
        // ever shifting the character off-screen or behind the menu panel.
        private void ApplyMicroSway()
        {
            if (_cam == null) return;

            var t = Time.time * swayRate * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Sin(t * 0.7f) * swayAmplitudeX, Mathf.Sin(t) * swayAmplitudeY, 0f);
            _cam.transform.position = _basePosition + offset;
            _cam.transform.LookAt(_baseLookAt);
        }
    }
}
