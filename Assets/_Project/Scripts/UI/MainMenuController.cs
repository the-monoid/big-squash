using Mirror;
using Steading.Player;
using Steading.Art;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Steading.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private string worldScenePath = "Assets/_Project/Scenes/World_Test.unity";

        private readonly Color[] _skinPalette =
        {
            new Color(0.70f, 0.50f, 0.37f),
            new Color(0.86f, 0.64f, 0.46f),
            new Color(0.52f, 0.36f, 0.25f),
            new Color(0.35f, 0.23f, 0.17f)
        };

        private readonly Color[] _hairPalette =
        {
            new Color(0.19f, 0.12f, 0.07f),
            new Color(0.54f, 0.35f, 0.16f),
            new Color(0.73f, 0.61f, 0.35f),
            new Color(0.06f, 0.055f, 0.05f)
        };

        private readonly Color[] _clothPalette =
        {
            new Color(0.13f, 0.32f, 0.27f),
            new Color(0.34f, 0.09f, 0.08f),
            new Color(0.18f, 0.24f, 0.40f),
            new Color(0.39f, 0.32f, 0.17f)
        };

        private readonly Color[] _pantsPalette =
        {
            new Color(0.15f, 0.19f, 0.24f),
            new Color(0.18f, 0.15f, 0.12f),
            new Color(0.26f, 0.26f, 0.23f),
            new Color(0.10f, 0.16f, 0.18f)
        };

        private readonly Color[] _cloakPalette =
        {
            new Color(0.15f, 0.23f, 0.34f),
            new Color(0.29f, 0.11f, 0.11f),
            new Color(0.24f, 0.29f, 0.18f),
            new Color(0.40f, 0.37f, 0.31f)
        };

        private CharacterCustomization _customization;
        private PlayerVisualAnimator _previewAnimator;
        private Transform _previewRoot;
        private InputField _nameField;
        private Text _statusText;
        private Font _font;
        private Material _stageWood;
        private Material _stageStone;
        private Material _stageGrass;
        private Material _stageIron;
        private Material _stageFire;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _customization = CharacterCustomization.LoadLocal();

            BuildStage();
            EnsureEventSystem();
            BuildPreviewCharacter();
            BuildCanvas();
            ApplyCustomizationToPreview();
        }

        private void Update()
        {
            if (_previewRoot == null) return;
            var sway = Mathf.Sin(Time.time * 0.55f) * 7f;
            _previewRoot.rotation = Quaternion.Euler(0f, 180f + sway, 0f);
        }

        private void BuildStage()
        {
            _stageGrass = CreateMaterial("MenuGrass", new Color(0.18f, 0.32f, 0.19f), 0.55f, 0f);
            _stageWood = CreateMaterial("MenuWeatheredWood", new Color(0.40f, 0.26f, 0.14f), 0.38f, 0f);
            _stageStone = CreateMaterial("MenuStone", new Color(0.43f, 0.43f, 0.39f), 0.62f, 0f);
            _stageIron = CreateMaterial("MenuDarkIron", new Color(0.16f, 0.17f, 0.17f), 0.42f, 0.25f);
            _stageFire = CreateMaterial("MenuFire", new Color(1.0f, 0.48f, 0.11f), 0.18f, 0f);

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.19f, 0.24f, 0.25f);
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.ambientLight = new Color(0.36f, 0.40f, 0.42f);

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                camera = cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
            }

            camera.transform.position = new Vector3(0.55f, 1.45f, -4.45f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.78f, 1.04f, 0.05f) - camera.transform.position, Vector3.up);
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.Skybox;

            var sun = new GameObject("Menu Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(42f, -31f, 0f);

            var fill = new GameObject("Forge Fill").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.55f, 0.25f);
            fill.intensity = 2.3f;
            fill.range = 5.5f;
            fill.transform.position = new Vector3(-1.4f, 0.85f, -0.8f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Menu Meadow Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = _stageGrass;

            CreateLonghouseBackdrop();
            CreateCampfire();
            CreateWeaponRack();
        }

        private void CreateLonghouseBackdrop()
        {
            for (int i = 0; i < 10; i++)
            {
                var plank = CreateBox("Backdrop Plank " + i, new Vector3(-2.8f + i * 0.42f, 0.72f, 1.15f), new Vector3(0.18f, 1.45f, 0.12f), _stageWood);
                plank.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(i * 1.7f) * 2.2f);
            }

            CreateBox("Backdrop Roof Beam", new Vector3(-0.9f, 1.58f, 1.07f), new Vector3(2.55f, 0.16f, 0.18f), _stageWood);
            CreateBox("Backdrop Base Beam", new Vector3(-0.9f, 0.08f, 1.05f), new Vector3(2.65f, 0.12f, 0.18f), _stageWood);
            CreateBox("Iron Banner Plate", new Vector3(-0.9f, 1.12f, 0.93f), new Vector3(0.55f, 0.20f, 0.035f), _stageIron);
        }

        private void CreateCampfire()
        {
            CreateBox("Fire Log A", new Vector3(-1.45f, 0.10f, -0.68f), new Vector3(0.48f, 0.08f, 0.08f), _stageWood).transform.rotation = Quaternion.Euler(0f, 32f, 0f);
            CreateBox("Fire Log B", new Vector3(-1.45f, 0.13f, -0.68f), new Vector3(0.48f, 0.08f, 0.08f), _stageWood).transform.rotation = Quaternion.Euler(0f, -35f, 0f);
            for (int i = 0; i < 8; i++)
            {
                var angle = i / 8f * Mathf.PI * 2f;
                var rock = CreateSphere("Fire Stone " + i, new Vector3(-1.45f + Mathf.Cos(angle) * 0.42f, 0.06f, -0.68f + Mathf.Sin(angle) * 0.32f), new Vector3(0.13f, 0.08f, 0.10f), _stageStone);
                rock.transform.rotation = Quaternion.Euler(0f, i * 27f, 0f);
            }

            var flame = CreateCone("Low Fire", new Vector3(-1.45f, 0.36f, -0.68f), new Vector3(0.23f, 0.54f, 0.23f), _stageFire);
            flame.transform.rotation = Quaternion.Euler(-4f, 0f, 5f);
        }

        private void CreateWeaponRack()
        {
            CreateBox("Rack Left Post", new Vector3(2.35f, 0.68f, 0.72f), new Vector3(0.09f, 1.25f, 0.09f), _stageWood);
            CreateBox("Rack Right Post", new Vector3(3.05f, 0.68f, 0.72f), new Vector3(0.09f, 1.25f, 0.09f), _stageWood);
            CreateBox("Rack Cross Beam", new Vector3(2.70f, 1.12f, 0.72f), new Vector3(0.82f, 0.08f, 0.08f), _stageWood);
            CreateBox("Rack Sword Blade", new Vector3(2.55f, 0.75f, 0.58f), new Vector3(0.035f, 0.68f, 0.025f), _stageIron);
            CreateBox("Rack Sword Grip", new Vector3(2.55f, 0.34f, 0.58f), new Vector3(0.13f, 0.05f, 0.05f), _stageWood);
            CreateBox("Rack Axe Handle", new Vector3(2.88f, 0.68f, 0.58f), new Vector3(0.035f, 0.78f, 0.035f), _stageWood).transform.rotation = Quaternion.Euler(0f, 0f, -12f);
            CreateBox("Rack Axe Head", new Vector3(2.77f, 1.05f, 0.58f), new Vector3(0.22f, 0.16f, 0.035f), _stageIron);
        }

        private void BuildPreviewCharacter()
        {
            var preview = new GameObject("Character Preview");
            preview.transform.position = new Vector3(1.05f, 0f, -0.15f);
            preview.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _previewRoot = preview.transform;
            _previewAnimator = preview.AddComponent<PlayerVisualAnimator>();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Main Menu Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.45f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var root = canvasGo.GetComponent<RectTransform>();
            Stretch(root);

            var menuPanel = CreatePanel("Creator Panel", root, new Color(0.045f, 0.052f, 0.052f, 0.92f));
            Anchor(menuPanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(455f, 0f));

            var title = CreateText("STEADING", menuPanel, 40, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.95f, 0.89f, 0.74f));
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -86f), new Vector2(-28f, -28f));

            var subtitle = CreateText("CHARACTER CREATION", menuPanel, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.71f, 0.76f, 0.70f));
            Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -118f), new Vector2(-30f, -88f));

            _nameField = CreateInput(menuPanel, _customization.characterName);
            Anchor(_nameField.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -180f), new Vector2(-34f, -134f));
            _nameField.onValueChanged.AddListener(value =>
            {
                _customization.characterName = value;
                ApplyCustomizationToPreview();
            });

            CreateArchetypeButtons(menuPanel, -244f);
            CreateSliderRow(menuPanel, "Height", -324f, 0.92f, 1.10f, _customization.heightScale, value =>
            {
                _customization.heightScale = value;
                ApplyCustomizationToPreview();
            });
            CreateSliderRow(menuPanel, "Build", -384f, 0.88f, 1.14f, _customization.buildScale, value =>
            {
                _customization.buildScale = value;
                ApplyCustomizationToPreview();
            });

            CreateSwatchRow(menuPanel, "Skin", -470f, _skinPalette, color =>
            {
                _customization.skinColor = color;
                ApplyCustomizationToPreview();
            });
            CreateSwatchRow(menuPanel, "Hair", -536f, _hairPalette, color =>
            {
                _customization.hairColor = color;
                ApplyCustomizationToPreview();
            });
            CreateSwatchRow(menuPanel, "Tunic", -602f, _clothPalette, color =>
            {
                _customization.tunicColor = color;
                ApplyCustomizationToPreview();
            });
            CreateSwatchRow(menuPanel, "Pants", -668f, _pantsPalette, color =>
            {
                _customization.pantsColor = color;
                ApplyCustomizationToPreview();
            });
            CreateSwatchRow(menuPanel, "Cloak", -734f, _cloakPalette, color =>
            {
                _customization.cloakColor = color;
                ApplyCustomizationToPreview();
            });

            CreateToggleRow(menuPanel, "Beard", -805f, _customization.beardEnabled, value =>
            {
                _customization.beardEnabled = value;
                ApplyCustomizationToPreview();
            });
            CreateToggleRow(menuPanel, "Helmet", -850f, _customization.helmetEnabled, value =>
            {
                _customization.helmetEnabled = value;
                ApplyCustomizationToPreview();
            });

            var start = CreateButton("START WORLD", menuPanel, StartHostGame, new Color(0.49f, 0.31f, 0.15f));
            Anchor(start.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(34f, 94f), new Vector2(-34f, 144f));

            var join = CreateButton("JOIN LOCALHOST", menuPanel, JoinLocalhost, new Color(0.19f, 0.29f, 0.30f));
            Anchor(join.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(34f, 34f), new Vector2(-8f, 80f));

            var quit = CreateButton("QUIT", menuPanel, () => Application.Quit(), new Color(0.24f, 0.20f, 0.18f));
            Anchor(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(8f, 34f), new Vector2(-34f, 80f));

            _statusText = CreateText("", root, 16, FontStyle.Normal, TextAnchor.LowerRight, new Color(0.84f, 0.86f, 0.82f));
            Anchor(_statusText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-560f, 30f), new Vector2(-34f, 74f));
        }

        private void CreateArchetypeButtons(RectTransform parent, float top)
        {
            CreateLabel(parent, "Archetype", top + 28f);
            var labels = new[] { "Raider", "Shieldbearer", "Woodsman" };
            for (int i = 0; i < labels.Length; i++)
            {
                var index = i;
                var button = CreateButton(labels[i], parent, () => ApplyArchetype(index), new Color(0.15f, 0.18f, 0.18f));
                Anchor(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f + i * 128f, top - 2f), new Vector2(148f + i * 128f, top + 40f));
            }
        }

        private void ApplyArchetype(int index)
        {
            switch (index)
            {
                case 1:
                    _customization.heightScale = 1.04f;
                    _customization.buildScale = 1.12f;
                    _customization.tunicColor = new Color(0.18f, 0.24f, 0.40f);
                    _customization.cloakColor = new Color(0.29f, 0.11f, 0.11f);
                    _customization.helmetEnabled = true;
                    break;
                case 2:
                    _customization.heightScale = 0.98f;
                    _customization.buildScale = 0.94f;
                    _customization.tunicColor = new Color(0.24f, 0.29f, 0.18f);
                    _customization.cloakColor = new Color(0.40f, 0.37f, 0.31f);
                    _customization.helmetEnabled = false;
                    break;
                default:
                    _customization.heightScale = 1.0f;
                    _customization.buildScale = 1.0f;
                    _customization.tunicColor = new Color(0.13f, 0.32f, 0.27f);
                    _customization.cloakColor = new Color(0.15f, 0.23f, 0.34f);
                    _customization.helmetEnabled = true;
                    break;
            }

            if (_nameField != null) _customization.characterName = _nameField.text;
            RebuildMenu();
        }

        private void RebuildMenu()
        {
            var canvas = GameObject.Find("Main Menu Canvas");
            if (canvas != null) Destroy(canvas);
            BuildCanvas();
            ApplyCustomizationToPreview();
        }

        private void CreateSliderRow(RectTransform parent, string label, float top, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            CreateLabel(parent, label, top + 22f);
            var sliderGo = new GameObject(label + " Slider");
            sliderGo.transform.SetParent(parent, false);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(value, min, max);
            slider.onValueChanged.AddListener(onChanged);
            Anchor(slider.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(125f, top), new Vector2(-34f, top + 28f));

            var background = CreatePanel("Background", slider.GetComponent<RectTransform>(), new Color(0.13f, 0.15f, 0.15f, 1f));
            Stretch(background);
            slider.targetGraphic = background.GetComponent<Image>();

            var fillArea = CreatePanel("Fill Area", slider.GetComponent<RectTransform>(), Color.clear);
            Anchor(fillArea, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            var fill = CreatePanel("Fill", fillArea, new Color(0.49f, 0.31f, 0.15f));
            Stretch(fill);
            slider.fillRect = fill;

            var handle = CreatePanel("Handle", slider.GetComponent<RectTransform>(), new Color(0.95f, 0.89f, 0.74f));
            Anchor(handle, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-9f, -13f), new Vector2(9f, 13f));
            slider.handleRect = handle;
        }

        private void CreateToggleRow(RectTransform parent, string label, float top, bool value, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var toggleGo = new GameObject(label + " Toggle");
            toggleGo.transform.SetParent(parent, false);
            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(onChanged);
            Anchor(toggle.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, top), new Vector2(230f, top + 32f));

            var box = CreatePanel("Box", toggle.GetComponent<RectTransform>(), new Color(0.13f, 0.15f, 0.15f));
            Anchor(box, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -12f), new Vector2(24f, 12f));
            var check = CreatePanel("Check", box, new Color(0.95f, 0.89f, 0.74f));
            Anchor(check, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = box.GetComponent<Image>();

            var text = CreateText(label, toggle.GetComponent<RectTransform>(), 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.83f, 0.86f, 0.79f));
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(36f, 0f), Vector2.zero);
        }

        private void CreateSwatchRow(RectTransform parent, string label, float top, Color[] colors, UnityEngine.Events.UnityAction<Color> onChosen)
        {
            CreateLabel(parent, label, top + 28f);
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                var button = CreateButton("", parent, () => onChosen(color), color);
                var rect = button.GetComponent<RectTransform>();
                Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f + i * 52f, top), new Vector2(164f + i * 52f, top + 40f));
            }
        }

        private void CreateLabel(RectTransform parent, string label, float top)
        {
            var text = CreateText(label, parent, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.70f, 0.76f, 0.70f));
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, top - 32f), new Vector2(124f, top));
        }

        private InputField CreateInput(RectTransform parent, string value)
        {
            var inputGo = new GameObject("Name Input");
            inputGo.transform.SetParent(parent, false);
            var image = inputGo.AddComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.14f, 1f);
            var input = inputGo.AddComponent<InputField>();
            input.targetGraphic = image;
            input.characterLimit = 18;

            var text = CreateText(value, inputGo.GetComponent<RectTransform>(), 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.94f, 0.91f, 0.82f));
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-12f, 0f));
            input.textComponent = text;
            input.text = value;

            var placeholder = CreateText("Name", inputGo.GetComponent<RectTransform>(), 18, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.48f, 0.52f, 0.49f));
            Anchor(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-12f, 0f));
            input.placeholder = placeholder;
            return input;
        }

        private Button CreateButton(string label, Transform parent, UnityEngine.Events.UnityAction action, Color color)
        {
            var go = new GameObject(string.IsNullOrEmpty(label) ? "Swatch Button" : label + " Button");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.20f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText(label, go.GetComponent<RectTransform>(), 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
                Stretch(text.rectTransform);
            }
            return button;
        }

        private Text CreateText(string value, Transform parent, int size, FontStyle style, TextAnchor alignment, Color color)
        {
            var go = new GameObject(value.Length == 0 ? "Text" : value + " Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = GetFont();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private void ApplyCustomizationToPreview()
        {
            if (_nameField != null) _customization.characterName = _nameField.text;
            _customization = _customization.Sanitized();
            if (_previewAnimator != null)
            {
                _previewAnimator.ApplyCustomization(_customization);
            }
        }

        private void StartHostGame()
        {
            SaveCustomization();
            var manager = GetNetworkManager();
            if (manager == null) return;
            manager.networkAddress = "localhost";
            manager.StartHost();
        }

        private void JoinLocalhost()
        {
            SaveCustomization();
            var manager = GetNetworkManager();
            if (manager == null) return;
            manager.networkAddress = "localhost";
            manager.StartClient();
        }

        private NetworkManager GetNetworkManager()
        {
            var manager = NetworkManager.singleton != null ? NetworkManager.singleton : FindFirstObjectByType<NetworkManager>();
            if (manager == null)
            {
                SetStatus("Network manager missing from menu scene.");
                return null;
            }

            if (playerPrefab != null) manager.playerPrefab = playerPrefab;
            manager.offlineScene = SceneManager.GetActiveScene().path;
            manager.onlineScene = worldScenePath;
            manager.autoCreatePlayer = true;
            SetStatus("Loading world...");
            return manager;
        }

        private void SaveCustomization()
        {
            if (_nameField != null) _customization.characterName = _nameField.text;
            _customization = _customization.Sanitized();
            _customization.SaveLocal();
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
        }

        private Font GetFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = new GameObject(name);
            go.name = name;
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateRoughBoxMesh(name + "Mesh", scale, ProceduralArt.StableSeed(name), Mathf.Min(0.025f, scale.magnitude * 0.012f), 3);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private GameObject CreateSphere(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = new GameObject(name);
            go.name = name;
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateRockMesh(name + "Mesh", scale * 0.5f, ProceduralArt.StableSeed(name), 7, 14, 0.26f);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private GameObject CreateCone(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = new GameObject(name);
            go.name = name;
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateConeMesh(name + "Mesh", scale.x, scale.y, ProceduralArt.StableSeed(name), 18, 0.16f);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private Material CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            return ProceduralArt.CreateLitMaterial(name, color, SurfaceForMaterial(name), smoothness, metallic);
        }

        private static ArtSurface SurfaceForMaterial(string name)
        {
            if (name.Contains("Wood")) return ArtSurface.Wood;
            if (name.Contains("Stone")) return ArtSurface.Stone;
            if (name.Contains("Grass")) return ArtSurface.Grass;
            if (name.Contains("Iron")) return ArtSurface.DarkMetal;
            if (name.Contains("Fire")) return ArtSurface.Banner;
            return ArtSurface.Plain;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
    }
}
