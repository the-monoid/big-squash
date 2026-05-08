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

        // Map size + resolution. 200m × 200m at 1.5m grid = 134×134 verts ≈ 18k.
        private const float MapSize = 200f;
        private const float CellSize = 1.5f;

        // Height curve: lerp(MinH, MaxH, fbm(noise))
        private const float MinHeight = -1.5f;
        private const float MaxHeight =  18f;
        private const float NoiseFreq = 0.018f;
        private const int   NoiseOctaves = 4;
        private const float NoiseLacunarity = 2.0f;
        private const float NoisePersist = 0.5f;

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
            n /= maxAmp;                              // 0..1
            // Sharpen the curve so flats are flatter and peaks are pointier.
            n = Mathf.SmoothStep(0f, 1f, n);
            return Mathf.Lerp(MinHeight, MaxHeight, n);
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
            // Color tiers (the painterly shader picks them up via vertex tint):
            //   sand/dirt   — low altitude
            //   grass       — mid altitude, low slope
            //   stone       — anywhere with high slope OR high altitude
            //   snow        — peaks
            Color32 grass  = new Color32(0x6E, 0x8E, 0x4A, 0xFF);
            Color32 dirt   = new Color32(0x6E, 0x57, 0x3A, 0xFF);
            Color32 stone  = new Color32(0x70, 0x70, 0x76, 0xFF);
            Color32 snow   = new Color32(0xE2, 0xE6, 0xE8, 0xFF);

            float steepBlend = Mathf.SmoothStep(0.35f, 0.85f, slope);
            float snowBlend  = Mathf.SmoothStep(0.65f * MaxHeight, 0.92f * MaxHeight, height);
            float dirtBlend  = Mathf.SmoothStep(0f, 1.5f, -height);   // below sea-ish

            // base = grass; lerp toward dirt at low altitude, stone with steepness, snow on peaks.
            Color c = grass;
            c = Color.Lerp(c, (Color)dirt, dirtBlend);
            c = Color.Lerp(c, (Color)stone, steepBlend);
            c = Color.Lerp(c, (Color)snow, snowBlend);
            return c;
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
