using Mirror;
using Steading.Building;
using Steading.Combat;
using Steading.Net;
using Steading.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.EditorTools
{
    public static class M3Setup
    {
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string ArtDir = "Assets/_Project/Art";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string WallPrefabPath = PrefabsDir + "/Wall.prefab";
        private const string FloorPrefabPath = PrefabsDir + "/Floor.prefab";
        private const string PillarPrefabPath = PrefabsDir + "/Pillar.prefab";
        private const string DoorwayPrefabPath = PrefabsDir + "/Doorway.prefab";
        private const string GhostValidMatPath = ArtDir + "/GhostValid.mat";
        private const string GhostInvalidMatPath = ArtDir + "/GhostInvalid.mat";
        private const string WoodMatPath = ArtDir + "/BuildableWood.mat";
        private const string StoneMatPath = ArtDir + "/BuildableStone.mat";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";

        [MenuItem("Steading/M3: Generate Building System (4 buildables, NavMesh-aware)")]
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
            var wood = GetOrCreateOpaqueColorMaterial(WoodMatPath, new Color(0.55f, 0.36f, 0.22f));
            var stone = GetOrCreateOpaqueColorMaterial(StoneMatPath, new Color(0.55f, 0.55f, 0.58f));

            var wallPrefab    = CreateBoxBuildable(WallPrefabPath,    "Wall",    new Vector3(2f, 3f, 0.2f),   200, wood, addNavObstacle: true);
            var floorPrefab   = CreateBoxBuildable(FloorPrefabPath,   "Floor",   new Vector3(2f, 0.2f, 2f),   180, wood, addNavObstacle: false);
            var pillarPrefab  = CreateBoxBuildable(PillarPrefabPath,  "Pillar",  new Vector3(0.4f, 3f, 0.4f), 250, stone, addNavObstacle: true);
            var doorwayPrefab = CreateDoorwayPrefab(DoorwayPrefabPath, wood);

            var entries = new[]
            {
                new BuildableEntry { label = "Wall",    prefab = wallPrefab,    halfExtents = new Vector3(1f, 1.5f, 0.1f) },
                new BuildableEntry { label = "Floor",   prefab = floorPrefab,   halfExtents = new Vector3(1f, 0.1f, 1f)   },
                new BuildableEntry { label = "Pillar",  prefab = pillarPrefab,  halfExtents = new Vector3(0.2f, 1.5f, 0.2f) },
                new BuildableEntry { label = "Doorway", prefab = doorwayPrefab, halfExtents = new Vector3(1f, 1.5f, 0.1f) },
            };

            UpdatePlayerPrefab(entries, ghostValid, ghostInvalid);

            RegisterPrefabsInNetworkManager(new[] { wallPrefab, floorPrefab, pillarPrefab, doorwayPrefab });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Steading M3 Setup",
                "Building system v2:\n" +
                "  • 4 buildables — Wall, Floor, Pillar, Doorway\n" +
                "  • All registered in NetworkBootstrap.spawnPrefabs\n" +
                "  • Walls + pillars carve the NavMesh — Draugr will path around them\n" +
                "  • Doorway has a passable opening (multi-collider, single Health)\n\n" +
                "Controls in build mode (B):\n" +
                "  • Tab — cycle buildable\n" +
                "  • R — rotate +90°\n" +
                "  • Left-click — place\n" +
                "  • Right-click — delete an existing structure",
                "OK");

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }

        // Builds a single-cube buildable (Wall, Floor, Pillar) with optional
        // NavMeshObstacle for runtime NavMesh carving.
        private static GameObject CreateBoxBuildable(string path, string name, Vector3 scale, int hp, Material mat, bool addNavObstacle)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = scale;
            root.GetComponent<MeshRenderer>().sharedMaterial = mat;

            if (addNavObstacle)
            {
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.center = Vector3.zero;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                obstacle.carvingMoveThreshold = 0.1f;
            }

            root.AddComponent<NetworkIdentity>();
            AddHealth(root, hp);
            root.AddComponent<Structure>();
            root.AddComponent<BuildableVisualEnhancer>();
            if (name == "Floor") root.AddComponent<WalkableSurface>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Doorway is three children — left jamb, right jamb, top header — each
        // with its own collider and NavMeshObstacle. The middle is open so
        // players can walk through. Single Health/Structure on the root.
        private static GameObject CreateDoorwayPrefab(string path, Material mat)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var root = new GameObject("Doorway");

            CreateDoorwaySection(root.transform, "LeftJamb",  new Vector3(0.4f, 3f, 0.2f),   new Vector3(-0.8f, 0f, 0f),    mat);
            CreateDoorwaySection(root.transform, "RightJamb", new Vector3(0.4f, 3f, 0.2f),   new Vector3( 0.8f, 0f, 0f),    mat);
            CreateDoorwaySection(root.transform, "Header",    new Vector3(1.2f, 0.5f, 0.2f), new Vector3( 0f, 1.25f, 0f),   mat);

            root.AddComponent<NetworkIdentity>();
            AddHealth(root, 220);
            root.AddComponent<Structure>();
            root.AddComponent<BuildableVisualEnhancer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateDoorwaySection(Transform parent, string name, Vector3 scale, Vector3 localPos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.center = Vector3.zero;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        private static void AddHealth(GameObject go, int maxHp)
        {
            var hp = go.AddComponent<Health>();
            var so = new SerializedObject(hp);
            so.FindProperty("maxHp").intValue = maxHp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdatePlayerPrefab(BuildableEntry[] entries, Material ghostValid, Material ghostInvalid)
        {
            using var edit = new PrefabUtility.EditPrefabContentsScope(PlayerPrefabPath);
            var root = edit.prefabContentsRoot;

            var bc = root.GetComponent<BuildController>() ?? root.AddComponent<BuildController>();
            if (root.GetComponent<BuildHud>() == null) root.AddComponent<BuildHud>();
            var so = new SerializedObject(bc);

            var arr = so.FindProperty("buildables");
            arr.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("label").stringValue = entries[i].label;
                elem.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                elem.FindPropertyRelative("halfExtents").vector3Value = entries[i].halfExtents;
            }

            so.FindProperty("ghostValidMat").objectReferenceValue = ghostValid;
            so.FindProperty("ghostInvalidMat").objectReferenceValue = ghostInvalid;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterPrefabsInNetworkManager(GameObject[] prefabs)
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var mgr = Object.FindFirstObjectByType<NetworkBootstrap>();
            if (mgr == null) return;

            var so = new SerializedObject(mgr);
            var prop = so.FindProperty("spawnPrefabs");
            if (prop == null) return;

            foreach (var prefab in prefabs)
            {
                bool present = false;
                for (int i = 0; i < prop.arraySize; i++)
                {
                    if (prop.GetArrayElementAtIndex(i).objectReferenceValue == prefab)
                    {
                        present = true;
                        break;
                    }
                }
                if (present) continue;

                prop.arraySize++;
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = prefab;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
        }

        private static Material GetOrCreateGhostMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                ApplyTransparentSettings(existing, color);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            EnsureDir(ArtDir);

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Hidden/Internal-Colored");
            var mat = new Material(shader);
            ApplyTransparentSettings(mat, color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material GetOrCreateOpaqueColorMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                ApplyOpaqueColor(existing, color);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            EnsureDir(ArtDir);

            var mat = new Material(FindOpaqueShader());
            ApplyOpaqueColor(mat, color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Shader FindOpaqueShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static void ApplyOpaqueColor(Material mat, Color color)
        {
            mat.shader = FindOpaqueShader();
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        private static void ApplyTransparentSettings(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
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
