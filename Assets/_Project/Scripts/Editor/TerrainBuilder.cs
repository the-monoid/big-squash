using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.EditorTools
{
    // Builds a real procedural mesh terrain to replace the flat plane in
    // World_Test.unity. Uses multi-octave Perlin noise for rolling Viking-style
    // hills, vertex-color paints grass / dirt / stone by slope + altitude, and
    // re-bakes the NavMesh on top.
    //
    // Run via Steading > Art: Generate Steading Terrain (large rolling map)
    public static class TerrainBuilder
    {
        private const string WorldScenePath = "Assets/_Project/Scenes/World_Test.unity";
        private const string MaterialPath   = "Assets/_Project/Art/Materials/Terrain.mat";
        private const string MeshAssetPath  = "Assets/_Project/Art/Meshes/SteadingTerrain.mesh";

        // Map size + resolution. 1024m × 1024m at 4m grid = 257×257 verts ≈ 66k.
        private const float MapSize = 1024f;
        private const float CellSize = 4f;

        // Height curve: lerp(MinH, MaxH, fbm(noise)) + ridge-noise mountain pass
        private const float MinHeight = -2.5f;
        private const float MaxHeight =  80f;
        private const float NoiseFreq = 0.005f;     // lower = wider rolling hills at this scale
        private const int   NoiseOctaves = 5;
        private const float NoiseLacunarity = 2.0f;
        private const float NoisePersist = 0.5f;

        // Mountain layer parameters (ridge noise added on top of base hills)
        private const float MountainFreq = 0.0035f;
        private const float MountainHeight = 70f;       // peaks reach ~70m above base
        private const float MountainThreshold = 0.55f;  // ridge value below this is flattened

        [MenuItem("Steading/Art: Generate Steading Terrain (large rolling map)")]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Terrain Builder",
                    "Stop Play mode and try again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

            // Remove old flat ground if present.
            var oldGround = GameObject.Find("Ground");
            if (oldGround != null) Object.DestroyImmediate(oldGround);

            // Build terrain mesh.
            var (mesh, mat) = BuildTerrainMeshAndMaterial();

            var terrainGo = new GameObject("Ground");
            terrainGo.layer = 0; // default; navmesh build picks all walkable static
            terrainGo.transform.position = Vector3.zero;
            var mf = terrainGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = terrainGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            var mc = terrainGo.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            terrainGo.isStatic = true;

            // Fresh NavMeshSurface bake covering the new ground.
            var nav = terrainGo.AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.All;
            nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            nav.layerMask = ~0;
            nav.BuildNavMesh();

            // Scatter Synty foliage by biome band.
            ScatterFoliage(terrainGo);

            // Move PlayerSpawn to a guaranteed-walkable point near origin.
            var spawn = GameObject.Find("PlayerSpawn");
            if (spawn == null)
            {
                spawn = new GameObject("PlayerSpawn");
                spawn.AddComponent<NetworkStartPosition>();
            }
            else if (spawn.GetComponent<NetworkStartPosition>() == null)
            {
                spawn.AddComponent<NetworkStartPosition>();
            }
            float ground = SampleHeight(0f, 0f);
            spawn.transform.position = new Vector3(0f, ground + 0.05f, 0f);

            // Add a sun light if none exists in the scene.
            EnsureSun();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("Terrain Builder",
                $"Generated {MapSize}×{MapSize}m mesh terrain (cell {CellSize}m), " +
                "vertex-painted grass / dirt / stone, NavMesh re-baked. " +
                "PlayerSpawn moved to ground level.",
                "OK");
        }

        // ---- mesh + material build ----

        private static (Mesh, Material) BuildTerrainMeshAndMaterial()
        {
            int verts = Mathf.CeilToInt(MapSize / CellSize) + 1;
            float origin = -MapSize * 0.5f;

            var positions = new Vector3[verts * verts];
            var uvs = new Vector2[verts * verts];
            var colors = new Color32[verts * verts];

            // Heightmap pass
            for (int z = 0; z < verts; z++)
            for (int x = 0; x < verts; x++)
            {
                float wx = origin + x * CellSize;
                float wz = origin + z * CellSize;
                float h = SampleHeight(wx, wz);
                int i = z * verts + x;
                positions[i] = new Vector3(wx, h, wz);
                uvs[i] = new Vector2(wx / 8f, wz / 8f);
            }

            // Triangles
            var tris = new List<int>((verts - 1) * (verts - 1) * 6);
            for (int z = 0; z < verts - 1; z++)
            for (int x = 0; x < verts - 1; x++)
            {
                int i = z * verts + x;
                int iRight = i + 1;
                int iUp = i + verts;
                int iUpRight = iUp + 1;
                tris.Add(i); tris.Add(iUp); tris.Add(iUpRight);
                tris.Add(i); tris.Add(iUpRight); tris.Add(iRight);
            }

            // Vertex paint based on slope + height
            for (int i = 0; i < positions.Length; i++)
            {
                float slope = ComputeSlope(positions, verts, i);
                colors[i] = PickVertexColor(positions[i].y, slope);
            }

            var mesh = new Mesh
            {
                indexFormat = positions.Length > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
                name = "SteadingTerrain"
            };
            mesh.vertices  = positions;
            mesh.uv        = uvs;
            mesh.colors32  = colors;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // Persist the mesh as a project asset so:
            //  - the scene file doesn't bloat with embedded geometry,
            //  - MeshCollider + NavMeshSurface re-bake cleanly across reloads,
            //  - we don't lose the terrain on a domain reload.
            EnsureFolder("Assets/_Project/Art/Meshes");
            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (existingMesh != null) AssetDatabase.DeleteAsset(MeshAssetPath);
            AssetDatabase.CreateAsset(mesh, MeshAssetPath);

            EnsureFolder("Assets/_Project/Art/Materials");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("Steading/PainterlyLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = "Terrain" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_VertexColorInfluence")) mat.SetFloat("_VertexColorInfluence", 1f);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            return (mesh, mat);
        }

        private static float SampleHeight(float x, float z)
        {
            // ---- Base rolling hills (multi-octave Perlin) ----
            float n = 0f;
            float amp = 1f;
            float freq = NoiseFreq;
            float maxAmp = 0f;
            for (int i = 0; i < NoiseOctaves; i++)
            {
                n += Mathf.PerlinNoise(x * freq + 1000f, z * freq + 1000f) * amp;
                maxAmp += amp;
                amp *= NoisePersist;
                freq *= NoiseLacunarity;
            }
            n /= maxAmp;                                  // 0..1
            n = Mathf.SmoothStep(0f, 1f, n);
            float baseHeight = Mathf.Lerp(0f, MaxHeight * 0.35f, n);

            // ---- Mountain layer: ridge noise (1 - |fbm - 0.5| * 2) ----
            // Produces sharp ridge lines characteristic of mountain ranges.
            // Thresholded so most of the map stays flat-ish; only ridges rise.
            float r = 0f;
            float rAmp = 1f;
            float rFreq = MountainFreq;
            float rMaxAmp = 0f;
            for (int i = 0; i < 4; i++)
            {
                float pn = Mathf.PerlinNoise(x * rFreq + 5000f, z * rFreq + 5000f);
                float ridge = 1f - Mathf.Abs(pn - 0.5f) * 2f;     // 0..1, peaks at ridge lines
                r += ridge * rAmp;
                rMaxAmp += rAmp;
                rAmp *= 0.55f;
                rFreq *= 2.1f;
            }
            r /= rMaxAmp;
            float mountainContribution = 0f;
            if (r > MountainThreshold)
            {
                float t = (r - MountainThreshold) / (1f - MountainThreshold);
                t = t * t;                                  // square for steeper falloff
                mountainContribution = t * MountainHeight;
            }

            float h = baseHeight + mountainContribution;
            // Sea-floor floor — anything way down gets a -2.5m bottom.
            return Mathf.Max(h, MinHeight);
        }

        private static float ComputeSlope(Vector3[] positions, int verts, int i)
        {
            int x = i % verts;
            int z = i / verts;
            int xL = Mathf.Max(x - 1, 0);
            int xR = Mathf.Min(x + 1, verts - 1);
            int zD = Mathf.Max(z - 1, 0);
            int zU = Mathf.Min(z + 1, verts - 1);

            float dy_x = positions[z * verts + xR].y - positions[z * verts + xL].y;
            float dy_z = positions[zU * verts + x].y - positions[zD * verts + x].y;
            float dx = (xR - xL) * CellSize;
            float dz = (zU - zD) * CellSize;
            float slope = Mathf.Sqrt((dy_x / Mathf.Max(0.001f, dx)) * (dy_x / Mathf.Max(0.001f, dx)) +
                                     (dy_z / Mathf.Max(0.001f, dz)) * (dy_z / Mathf.Max(0.001f, dz)));
            return slope;     // ~0 flat, ~1+ steep
        }

        private static Color32 PickVertexColor(float height, float slope)
        {
            // Biome bands by altitude (Valheim-leaning palette):
            //   beach        -2.5m → 1m   warm tan
            //   plains/grass 1m → 12m     bright green
            //   forest       12m → 28m    deeper green (the woodland belt)
            //   stone        28m → 55m    slate grey
            //   snow         55m+ or steep   off-white
            Color32 beach     = new Color32(0xCC, 0xB6, 0x8A, 0xFF);
            Color32 plains    = new Color32(0x78, 0x99, 0x4A, 0xFF);
            Color32 forest    = new Color32(0x4A, 0x6E, 0x32, 0xFF);
            Color32 stone     = new Color32(0x68, 0x6C, 0x70, 0xFF);
            Color32 snow      = new Color32(0xE2, 0xE6, 0xE8, 0xFF);

            // Altitude blend factors
            float beachT  = Mathf.SmoothStep(1f, -1f, height);                                // 1m → 0
            float plainsT = Mathf.SmoothStep(2f, 12f, height) * (1f - Mathf.SmoothStep(12f, 22f, height));
            float forestT = Mathf.SmoothStep(12f, 22f, height) * (1f - Mathf.SmoothStep(28f, 38f, height));
            float stoneT  = Mathf.SmoothStep(28f, 40f, height);
            float snowT   = Mathf.SmoothStep(48f, 65f, height);

            // Slope override: steep faces go stone regardless of altitude.
            float slopeStone = Mathf.SmoothStep(0.45f, 0.9f, slope);

            // Composite: start from plains, layer by largest weight.
            Color c = (Color)plains;
            c = Color.Lerp(c, (Color)beach,  beachT);
            c = Color.Lerp(c, (Color)forest, forestT);
            c = Color.Lerp(c, (Color)stone,  Mathf.Max(stoneT, slopeStone));
            c = Color.Lerp(c, (Color)snow,   snowT);
            return c;
        }

        // ---- Foliage scatter ----

        private static readonly string[] TreePrefabPaths =
        {
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Tree_01.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Tree_02.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Tree_03.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Tree_04.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_TreeDead_01.prefab",
        };
        private static readonly string[] RockPrefabPaths =
        {
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Small_Rocks_01.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Small_Rocks_02.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Small_Rocks_03.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Small_Rocks_04.prefab",
            "Assets/Synty/PolygonStarter/Prefabs/SM_Generic_Small_Rocks_05.prefab",
        };

        private static void ScatterFoliage(GameObject terrainGo)
        {
            var trees = LoadPrefabs(TreePrefabPaths);
            var rocks = LoadPrefabs(RockPrefabPaths);
            if (trees.Count == 0 && rocks.Count == 0)
            {
                Debug.LogWarning("[Steading] No Synty foliage/rock prefabs found — skip scatter.");
                return;
            }

            // Reproducible scatter via a fixed seed so the world looks the
            // same each rebuild.
            var rand = new System.Random(31337);
            var scatterRoot = new GameObject("Foliage_Scatter").transform;
            scatterRoot.SetParent(terrainGo.transform, worldPositionStays: false);

            // ~150 trees in the forest band (12-28m altitude) + plains (4-12m)
            int placed = 0;
            int attempts = 0;
            while (placed < 180 && attempts < 1500)
            {
                attempts++;
                float x = ((float)rand.NextDouble() - 0.5f) * MapSize * 0.95f;
                float z = ((float)rand.NextDouble() - 0.5f) * MapSize * 0.95f;
                float y = SampleHeight(x, z);
                if (y < 1.5f || y > 35f) continue; // skip beach + high stone
                var prefab = trees[rand.Next(trees.Count)];
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scatterRoot);
                inst.transform.position = new Vector3(x, y - 0.05f, z);
                inst.transform.rotation = Quaternion.Euler(0f, (float)rand.NextDouble() * 360f, 0f);
                float scl = 0.85f + (float)rand.NextDouble() * 0.45f;
                inst.transform.localScale = new Vector3(scl, scl, scl);
                placed++;
            }

            // ~60 rocks in the stone band + on slopes
            placed = 0;
            attempts = 0;
            while (placed < 60 && attempts < 800)
            {
                attempts++;
                float x = ((float)rand.NextDouble() - 0.5f) * MapSize * 0.95f;
                float z = ((float)rand.NextDouble() - 0.5f) * MapSize * 0.95f;
                float y = SampleHeight(x, z);
                if (y < 0f) continue;
                var prefab = rocks[rand.Next(rocks.Count)];
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scatterRoot);
                inst.transform.position = new Vector3(x, y, z);
                inst.transform.rotation = Quaternion.Euler(0f, (float)rand.NextDouble() * 360f, 0f);
                float scl = 0.7f + (float)rand.NextDouble() * 1.6f;
                inst.transform.localScale = new Vector3(scl, scl, scl);
                placed++;
            }
        }

        private static System.Collections.Generic.List<GameObject> LoadPrefabs(string[] paths)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var p in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) list.Add(go);
            }
            return list;
        }

        private static void EnsureSun()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) return; // already a sun
            }
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.93f, 0.78f);
            light.intensity = 1.45f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, -38f, 0f);
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
    }

}
