using System.Collections.Generic;
using Mirror;
using Steading.AI;
using Steading.AI.Archetypes;
using Steading.Combat;
using Steading.Net;
using Steading.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.EditorTools
{
    public static class M2Setup
    {
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string DraugrPrefabPath = PrefabsDir + "/Draugr.prefab";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string WorldScenePath = "Assets/_Project/Scenes/World_Test.unity";

        [MenuItem("Steading/M2: Generate Combat (Health, Draugr, NavMesh)")]
        public static void GenerateAll()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Steading M2 Setup",
                    "Cannot run while Play mode is active. Stop Play (Ctrl+P) and try again.", "OK");
                return;
            }

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath))
            {
                EditorUtility.DisplayDialog("Steading M2 Setup",
                    "Player.prefab not found. Run 'Steading > M1: Generate Bootstrap, World, and Player' first.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UpdatePlayerPrefab();
            var draugrPrefab = CreateDraugrPrefab();
            RegisterDraugrInNetworkManager(draugrPrefab);
            BakeWorldNavMeshAndSpawner(draugrPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Steading M2 Setup",
                "Combat ready:\n" +
                "  • Player has Health, PlayerAttack (left-click), PlayerRespawn\n" +
                "  • Draugr.prefab created (HP 60, melee chaser)\n" +
                "  • Draugr registered in NetworkBootstrap.spawnPrefabs\n" +
                "  • World_Test has a NavMesh and an EnemySpawner (3 draugr)\n\n" +
                "Press Play in Bootstrap, click Host. Three draugr will spawn in a ring " +
                "and chase you. Left-click attacks; you respawn 2s after dying.",
                "OK");

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }

        private static void UpdatePlayerPrefab()
        {
            var path = PlayerPrefabPath;
            using var edit = new PrefabUtility.EditPrefabContentsScope(path);
            var root = edit.prefabContentsRoot;

            EnsureComponent<Health>(root);
            EnsureComponent<PlayerAttack>(root);
            EnsureComponent<PlayerRespawn>(root);
            EnsureComponent<PlayerAnimatorBridge>(root);
            EnsureComponent<PlayerInventory>(root);
        }

        private static GameObject CreateDraugrPrefab()
        {
            var root = new GameObject("Draugr");

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            // Tint the renderer dark grey so it reads as an enemy at a glance.
            var mr = visual.GetComponent<MeshRenderer>();
            var draugrMat = GetOrCreateColorMaterial("DraugrSkin", new Color(0.32f, 0.32f, 0.34f));
            mr.sharedMaterial = draugrMat;
            mr.enabled = false;

            // CapsuleCollider on root so PlayerAttack raycasts hit, and so the player's
            // CharacterController bumps the Draugr instead of walking through it.
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.4f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.8f;
            agent.radius = 0.4f;
            agent.speed = 3.2f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1.5f;
            agent.autoBraking = true;

            root.AddComponent<NetworkIdentity>();
            var nt = root.AddComponent<NetworkTransformReliable>();
            nt.syncDirection = SyncDirection.ServerToClient;
            root.AddComponent<EnemyActor>();

            var hp = root.AddComponent<Health>();
            var hpSo = new SerializedObject(hp);
            hpSo.FindProperty("maxHp").intValue = 60;
            hpSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<Draugr>();
            root.AddComponent<EnemyVisualAnimator>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DraugrPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void RegisterDraugrInNetworkManager(GameObject draugrPrefab)
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var mgr = Object.FindFirstObjectByType<NetworkBootstrap>();
            if (mgr == null)
            {
                Debug.LogWarning("[Steading] NetworkBootstrap not found in Bootstrap.unity — skip prefab registration.");
                return;
            }

            var so = new SerializedObject(mgr);
            var prefabsProp = so.FindProperty("spawnPrefabs");
            if (prefabsProp == null) return;

            // Avoid duplicate entries.
            for (int i = 0; i < prefabsProp.arraySize; i++)
            {
                var elem = prefabsProp.GetArrayElementAtIndex(i);
                if (elem.objectReferenceValue == draugrPrefab) return;
            }

            prefabsProp.arraySize++;
            prefabsProp.GetArrayElementAtIndex(prefabsProp.arraySize - 1).objectReferenceValue = draugrPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
        }

        private static void BakeWorldNavMeshAndSpawner(GameObject draugrPrefab)
        {
            var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                Debug.LogWarning("[Steading] Ground GameObject not found in World_Test — NavMesh bake skipped.");
                return;
            }

            var surface = ground.GetComponent<NavMeshSurface>();
            if (surface == null) surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // Replace any existing spawner so the prefab/count stay in sync.
            var existingSpawner = GameObject.Find("EnemySpawner");
            if (existingSpawner != null) Object.DestroyImmediate(existingSpawner);

            var spawner = new GameObject("EnemySpawner");
            spawner.transform.position = new Vector3(0f, 0f, 8f);
            var es = spawner.AddComponent<EnemySpawner>();
            var so = new SerializedObject(es);
            so.FindProperty("enemyPrefab").objectReferenceValue = draugrPrefab;
            so.FindProperty("count").intValue = 4;
            so.FindProperty("radius").floatValue = 9f;
            so.FindProperty("spawnFromForts").boolValue = true;
            so.FindProperty("fortSpawnRadius").floatValue = 7.5f;
            so.FindProperty("waveInterval").floatValue = 28f;
            so.FindProperty("addPerWave").intValue = 1;
            so.FindProperty("maxAlive").intValue = 14;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static Material GetOrCreateColorMaterial(string name, Color color)
        {
            var path = $"Assets/_Project/Art/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                ApplyOpaqueColor(existing, color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            var mat = new Material(FindRenderableShader()) { name = name };
            ApplyOpaqueColor(mat, color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Shader FindRenderableShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static void ApplyOpaqueColor(Material mat, Color color)
        {
            mat.shader = FindRenderableShader();
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
    }
}
