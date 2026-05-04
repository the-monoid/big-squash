using Mirror;
using Steading.Building;
using Steading.Combat;
using Steading.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Steading.EditorTools
{
    public static class M3Setup
    {
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string ArtDir = "Assets/_Project/Art";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string WallPrefabPath = PrefabsDir + "/Wall.prefab";
        private const string GhostValidMatPath = ArtDir + "/GhostValid.mat";
        private const string GhostInvalidMatPath = ArtDir + "/GhostInvalid.mat";
        private const string WallMatPath = ArtDir + "/Wall.mat";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";

        [MenuItem("Steading/M3: Generate Building System (Wall + BuildController)")]
        public static void GenerateAll()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Steading M3 Setup",
                    "Cannot run while Play mode is active. Stop Play (Ctrl+P) and try again.", "OK");
                return;
            }

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath))
            {
                EditorUtility.DisplayDialog("Steading M3 Setup",
                    "Player.prefab not found. Run M1 setup first.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var ghostValid = GetOrCreateGhostMaterial(GhostValidMatPath, new Color(0.3f, 1f, 0.3f, 0.45f));
            var ghostInvalid = GetOrCreateGhostMaterial(GhostInvalidMatPath, new Color(1f, 0.3f, 0.3f, 0.45f));
            var wallMat = GetOrCreateOpaqueColorMaterial(WallMatPath, new Color(0.55f, 0.36f, 0.22f));

            var wallPrefab = CreateWallPrefab(wallMat);

            UpdatePlayerPrefab(wallPrefab, ghostValid, ghostInvalid);
            RegisterWallInNetworkManager(wallPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Steading M3 Setup",
                "Building system ready:\n" +
                "  • Wall.prefab (200 HP, 2 x 3 x 0.2m)\n" +
                "  • Player has BuildController\n" +
                "  • Wall registered in NetworkBootstrap.spawnPrefabs\n\n" +
                "In Play mode, press B to toggle build mode. Look at the ground, " +
                "left-click to place a wall, right-click an existing wall to delete. " +
                "Walls have HP — Draugr will smash through them.",
                "OK");

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }

        private static GameObject CreateWallPrefab(Material wallMat)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            if (existing != null) AssetDatabase.DeleteAsset(WallPrefabPath);

            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Wall";
            root.transform.localScale = new Vector3(2f, 3f, 0.2f);

            var mr = root.GetComponent<MeshRenderer>();
            mr.sharedMaterial = wallMat;

            root.AddComponent<NetworkIdentity>();

            var hp = root.AddComponent<Health>();
            var hpSo = new SerializedObject(hp);
            hpSo.FindProperty("maxHp").intValue = 200;
            hpSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<Structure>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, WallPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void UpdatePlayerPrefab(GameObject wallPrefab, Material ghostValid, Material ghostInvalid)
        {
            using var edit = new PrefabUtility.EditPrefabContentsScope(PlayerPrefabPath);
            var root = edit.prefabContentsRoot;

            var bc = root.GetComponent<BuildController>() ?? root.AddComponent<BuildController>();

            var so = new SerializedObject(bc);

            var buildables = so.FindProperty("buildables");
            buildables.arraySize = 1;
            buildables.GetArrayElementAtIndex(0).objectReferenceValue = wallPrefab;

            var halfExtents = so.FindProperty("buildableHalfExtents");
            halfExtents.arraySize = 1;
            halfExtents.GetArrayElementAtIndex(0).vector3Value = new Vector3(1f, 1.5f, 0.1f);

            so.FindProperty("ghostValidMat").objectReferenceValue = ghostValid;
            so.FindProperty("ghostInvalidMat").objectReferenceValue = ghostInvalid;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterWallInNetworkManager(GameObject wallPrefab)
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var mgr = Object.FindFirstObjectByType<NetworkBootstrap>();
            if (mgr == null) return;

            var so = new SerializedObject(mgr);
            var prefabs = so.FindProperty("spawnPrefabs");
            if (prefabs == null) return;

            for (int i = 0; i < prefabs.arraySize; i++)
            {
                if (prefabs.GetArrayElementAtIndex(i).objectReferenceValue == wallPrefab) return;
            }

            prefabs.arraySize++;
            prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = wallPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
        }

        private static Material GetOrCreateGhostMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            EnsureDir(ArtDir);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            ApplyTransparentSettings(mat, color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material GetOrCreateOpaqueColorMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            EnsureDir(ArtDir);

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void ApplyTransparentSettings(Material mat, Color color)
        {
            // URP/Unlit transparent surface configuration. Set the property and the
            // matching keyword + render-queue so the URP shader takes the transparent
            // path at runtime.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // 0 = Alpha
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void EnsureDir(string assetPath)
        {
            var dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        }
    }
}
