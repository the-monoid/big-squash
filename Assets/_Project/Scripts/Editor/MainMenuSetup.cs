using System.Collections.Generic;
using System.IO;
using kcp2k;
using Mirror;
using Steading.Net;
using Steading.Player;
using Steading.UI;
// MainMenuPresenter lives in Steading.UI as well — this using imports both.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Steading.EditorTools
{
    public static class MainMenuSetup
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string MainMenuScenePath = ScenesDir + "/MainMenu.unity";
        private const string BootstrapScenePath = ScenesDir + "/Bootstrap.unity";
        private const string WorldScenePath = ScenesDir + "/World_Test.unity";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";

        [MenuItem("Steading/Generate Main Menu and Character Creator")]
        public static void Generate()
        {
            GenerateInternal(showDialog: true);
        }

        public static void GenerateFromCommandLine()
        {
            GenerateInternal(showDialog: false);
        }

        private static void GenerateInternal(bool showDialog)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Steading] Cannot generate main menu while Play mode is active.");
                return;
            }

            if (showDialog && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Steading] Main menu setup cancelled.");
                return;
            }

            EnsureDir(ScenesDir);
            EnsureDir(PrefabsDir);

            var playerPrefab = EnsurePlayerAppearanceOnPrefab();
            CreateMainMenuScene(playerPrefab);
            UpdateBootstrapScene(playerPrefab);
            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Steading Main Menu",
                    "Created MainMenu.unity with character creation and added it as the first build scene.",
                    "OK");
                EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            }
            else
            {
                Debug.Log("[Steading] Main menu and character creator generated.");
            }
        }

        private static GameObject EnsurePlayerAppearanceOnPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Steading] Player.prefab not found. Run M1 setup before main menu setup.");
                return null;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root.GetComponent<PlayerAppearance>() == null)
            {
                root.AddComponent<PlayerAppearance>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[Steading] Added PlayerAppearance to Player.prefab.");
            }
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        }

        private static void CreateMainMenuScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera();
            CreateMenuNetworkManager(playerPrefab);
            CreateEventSystem();
            CreateMainMenuController(playerPrefab);
            CreateMainMenuPresenter();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateMainCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0.55f, 1.45f, -4.45f);
            cameraGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.78f, 1.04f, 0.05f) - cameraGo.transform.position, Vector3.up);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 48f;
            cameraGo.AddComponent<AudioListener>();
        }

        private static void CreateMenuNetworkManager(GameObject playerPrefab)
        {
            var go = new GameObject("NetworkBootstrap");
            var transport = go.AddComponent<KcpTransport>();
            transport.port = 7777;

            var manager = go.AddComponent<NetworkBootstrap>();
            manager.transport = transport;
            manager.playerPrefab = playerPrefab;
            manager.onlineScene = WorldScenePath;
            manager.offlineScene = MainMenuScenePath;
            manager.autoCreatePlayer = true;
            manager.dontDestroyOnLoad = true;
            manager.runInBackground = true;
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static void CreateMainMenuController(GameObject playerPrefab)
        {
            var go = new GameObject("MainMenuController");
            var controller = go.AddComponent<MainMenuController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            serialized.FindProperty("worldScenePath").stringValue = WorldScenePath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateMainMenuPresenter()
        {
            var go = new GameObject("MainMenuPresenter");
            go.AddComponent<MainMenuPresenter>();
            // Settings on the presenter resolve target/camera at runtime.

            // Move the painterly sky into Resources/ so the presenter can load it
            // from a built player too, not just the editor.
            EnsureFolder("Assets/_Project/Resources");
            var src = "Assets/_Project/Art/Materials/PainterlySky.mat";
            var dst = "Assets/_Project/Resources/PainterlySky.mat";
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), src)) &&
                !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), dst)))
            {
                AssetDatabase.CopyAsset(src, dst);
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var slash = assetPath.LastIndexOf('/');
            var parent = slash >= 0 ? assetPath.Substring(0, slash) : "Assets";
            var name = slash >= 0 ? assetPath.Substring(slash + 1) : assetPath;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void UpdateBootstrapScene(GameObject playerPrefab)
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), BootstrapScenePath))) return;

            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var manager = Object.FindFirstObjectByType<NetworkBootstrap>();
            if (manager != null)
            {
                manager.offlineScene = MainMenuScenePath;
                manager.onlineScene = WorldScenePath;
                manager.playerPrefab = playerPrefab;
                EditorUtility.SetDirty(manager);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AddScenesToBuildSettings()
        {
            var ordered = new List<string> { MainMenuScenePath, WorldScenePath, BootstrapScenePath };
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (!ordered.Contains(existing.path)) ordered.Add(existing.path);
            }

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in ordered)
            {
                if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path)))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureDir(string assetPath)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        }
    }
}
