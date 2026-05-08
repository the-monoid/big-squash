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
        private const string ArtDir = "Assets/_Project/Art";
        private const string BootstrapScenePath = ScenesDir + "/Bootstrap.unity";
        private const string WorldScenePath = ScenesDir + "/World_Test.unity";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string PillTexturePath = ArtDir + "/CapsulePill.png";
        private const string PillMaterialPath = ArtDir + "/CapsulePill.mat";

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
            EnsureDir(ArtDir);

            var pillMaterial = GetOrCreatePillMaterial();
            var playerPrefab = CreatePlayerPrefab(pillMaterial);
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

        private const string ImportedVikingHeroPath = "Assets/_Project/Art/Models/Characters/Player/Player_VikingHero.fbx";
        private const string PlayerAnimatorPath     = "Assets/_Project/Animation/PlayerAnimator.controller";

        private static GameObject CreatePlayerPrefab(Material visualMaterial)
        {
            var root = new GameObject("Player");
            root.transform.position = Vector3.zero;

            // Try the imported Mixamo VikingHero rig first. Fall back to the old
            // procedural visual only if the FBX hasn't landed yet.
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedVikingHeroPath);
            if (imported != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(imported);
                visual.name = "VisualRig";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                // The Mixamo X Bot rig comes already at ~1.85m tall in T-pose; we
                // don't need to scale it. The Animator on the imported instance is
                // the one we drive — wire the controller asset and the avatar.
                //
                // Force-setting the avatar is essential. Without it, even if the
                // Animator instance has its FBX-imported avatar, runtime can end
                // up with a null avatar reference (especially when InstantiatePrefab
                // strips it under certain FBX configurations) and Mecanim falls
                // back to T-pose silently.
                var anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
                if (controller != null) anim.runtimeAnimatorController = controller;
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ImportedVikingHeroPath);
                if (avatar != null) anim.avatar = avatar;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // never go T-pose when off-screen
            }
            else
            {
                // Fallback for the pre-Phase-0 / pre-Mixamo state — a plain capsule
                // with the painterly material so the project still works if assets
                // aren't imported.
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "VisualFallback";
                Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                if (visualMaterial != null)
                {
                    visual.GetComponent<MeshRenderer>().sharedMaterial = visualMaterial;
                }
            }

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.radius = 0.4f;

            root.AddComponent<NetworkIdentity>();
            var nt = root.AddComponent<NetworkTransformReliable>();
            nt.syncDirection = SyncDirection.ClientToServer;

            root.AddComponent<PlayerInput>();

            // PlayerVisualAnimator (procedural blob rig) is no longer added — the
            // bridge drives the imported FBX's Animator instead. Bridge auto-finds
            // it via GetComponentInChildren so we don't have to move the Animator.
            root.AddComponent<PlayerAnimatorBridge>();

            root.AddComponent<PlayerAppearance>();
            root.AddComponent<PlayerInventory>();
            root.AddComponent<PlayerController>();

            // Hand-rolled third-person camera. PlayerCameraRig drives Camera.main
            // directly on the local player — no Cinemachine, no prefab, no Brain.
            root.AddComponent<PlayerCameraRig>();

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

        // Generates a horizontal two-tone texture (orange bottom, white top) and
        // a URP/Lit material referencing it. Result: capsule mesh looks like a
        // pharmaceutical pill capsule when applied. Capsule UVs map V along the
        // mesh's Y axis, so a horizontal stripe in the texture becomes a
        // horizontal band on the capsule at the equator.
        private static Material GetOrCreatePillMaterial()
        {
            // Idempotent — return existing if we've already generated it.
            var existing = AssetDatabase.LoadAssetAtPath<Material>(PillMaterialPath);
            if (existing != null)
            {
                ApplyOpaqueMaterialSettings(existing, AssetDatabase.LoadAssetAtPath<Texture2D>(PillTexturePath), 0.55f);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            // Build the texture in memory: 4-px wide x 256 tall, hard split at the midline.
            const int width = 4;
            const int height = 256;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false);
            var bottom = new Color32(0xE6, 0x6B, 0x2C, 0xFF); // warm orange
            var top    = new Color32(0xF8, 0xF8, 0xF8, 0xFF); // off-white
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                var color = (y < height / 2) ? bottom : top;
                for (int x = 0; x < width; x++) pixels[y * width + x] = color;
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);

            // Persist as PNG asset so the prefab can reference it.
            var pngBytes = tex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), PillTexturePath), pngBytes);
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(PillTexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(PillTexturePath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;     // keep the stripe edge sharp
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PillTexturePath);

            var mat = new Material(FindRenderableShader()) { name = "CapsulePill" };
            ApplyOpaqueMaterialSettings(mat, loadedTex, 0.55f);

            AssetDatabase.CreateAsset(mat, PillMaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Shader FindRenderableShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static void ApplyOpaqueMaterialSettings(Material mat, Texture2D texture, float smoothness)
        {
            mat.shader = FindRenderableShader();
            if (texture != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (texture != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }
}
