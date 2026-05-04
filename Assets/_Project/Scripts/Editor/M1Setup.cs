using System.IO;
using kcp2k;
using Mirror;
using Steading.Net;
using Steading.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Steading.EditorTools
{
    public static class M1Setup
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string BootstrapScenePath = ScenesDir + "/Bootstrap.unity";
        private const string WorldScenePath = ScenesDir + "/World_Test.unity";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";

        [MenuItem("Steading/M1: Generate Bootstrap, World, and Player")]
        public static void GenerateAll()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Steading M1 Setup",
                    "Cannot run while Play mode is active. Stop Play (Ctrl+P) and try again.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Steading] M1 setup cancelled (unsaved scene changes).");
                return;
            }

            EnsureDir(ScenesDir);
            EnsureDir(PrefabsDir);

            var playerPrefab = CreatePlayerPrefab();
            CreateWorldTestScene();
            CreateBootstrapScene(playerPrefab);
            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Steading M1 Setup",
                "Created:\n  • Bootstrap.unity (NetworkManager + transport + HUD)\n  • World_Test.unity (ground + light)\n  • Player.prefab (CharacterController + NetworkIdentity)\n\n" +
                "Both scenes added to Build Settings.\n\n" +
                "Next: open Bootstrap.unity, press Play, click 'Host (Server + Client)' in the on-screen HUD.",
                "OK");

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }

        private static GameObject CreatePlayerPrefab()
        {
            var root = new GameObject("Player");
            root.transform.position = Vector3.zero;

            // Visual: capsule with collider stripped (CharacterController handles collision).
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);

            // Camera pivot at head height for first-person view.
            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.7f, 0f);

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.radius = 0.4f;

            root.AddComponent<NetworkIdentity>();
            var nt = root.AddComponent<NetworkTransformReliable>();
            nt.syncDirection = SyncDirection.ClientToServer;

            root.AddComponent<PlayerInput>();
            var pc = root.AddComponent<PlayerController>();
            var so = new SerializedObject(pc);
            so.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateWorldTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            CreateMainCamera(new Vector3(0f, 5f, -8f), Quaternion.Euler(20f, 0f, 0f));

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = Vector3.zero;
            spawn.AddComponent<NetworkStartPosition>();

            EditorSceneManager.SaveScene(scene, WorldScenePath);
        }

        private static void CreateBootstrapScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(new Vector3(0f, 1f, -3f), Quaternion.identity);

            var nm = new GameObject("NetworkBootstrap");
            var transport = nm.AddComponent<KcpTransport>();
            transport.port = 7777;

            var bootstrap = nm.AddComponent<NetworkBootstrap>();
            bootstrap.transport = transport;
            bootstrap.playerPrefab = playerPrefab;
            bootstrap.onlineScene = WorldScenePath;
            bootstrap.offlineScene = BootstrapScenePath;
            bootstrap.autoCreatePlayer = true;

            nm.AddComponent<NetworkManagerHUD>();

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void AddScenesToBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(WorldScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
        }

        private static void EnsureDir(string assetPath)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        }

        private static void CreateMainCamera(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = position;
            go.transform.rotation = rotation;
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            go.AddComponent<AudioListener>();
        }
    }
}
