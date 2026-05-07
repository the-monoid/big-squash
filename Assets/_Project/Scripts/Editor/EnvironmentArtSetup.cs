using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Steading.EditorTools
{
    public static class EnvironmentArtSetup
    {
        private const string ArtDir = "Assets/_Project/Art";
        private const string GeneratedDir = ArtDir + "/Generated";
        private const string FloorPrefabPath = "Assets/_Project/Prefabs/Floor.prefab";
        private const string WorldScenePath = "Assets/_Project/Scenes/World_Test.unity";

        private const string FloorTileTexturePath = GeneratedDir + "/HearthStoneTiles.png";
        private const string TerrainTexturePath = GeneratedDir + "/MeadowTerrain.png";
        private const string FloorTileMaterialPath = ArtDir + "/HearthStoneTiles.mat";
        private const string TerrainMaterialPath = ArtDir + "/MeadowTerrain.mat";
        private const string TerrainMeshPath = GeneratedDir + "/World_Test_Terrain.mesh";
        private const string HearthPadMeshPath = GeneratedDir + "/HearthStonePad.mesh";

        [MenuItem("Steading/Art: Generate Floor Tiles and Terrain")]
        public static void GenerateAll()
        {
            var showDialogs = !Application.isBatchMode;
            if (Application.isPlaying)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Steading Environment Art",
                        "Cannot run while Play mode is active. Stop Play mode and try again.",
                        "OK");
                }
                else
                {
                    Debug.LogError("[Steading] Environment art generation cannot run while Play mode is active.");
                }
                return;
            }

            if (showDialogs && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureDir(GeneratedDir);

            var floorTexture = CreateFloorTileTexture(FloorTileTexturePath);
            var terrainTexture = CreateTerrainTexture(TerrainTexturePath);
            var floorMaterial = CreateLitMaterial(FloorTileMaterialPath, floorTexture, Color.white, 0.35f);
            var terrainMaterial = CreateLitMaterial(TerrainMaterialPath, terrainTexture, Color.white, 0.18f);
            var terrainMesh = CreateTerrainMesh(TerrainMeshPath);
            var hearthPadMesh = CreateHearthPadMesh(HearthPadMeshPath);

            ApplyFloorMaterial(floorMaterial);
            ApplyWorldTerrain(terrainMesh, terrainMaterial, hearthPadMesh, floorMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var message =
                "Generated floor tile and terrain rendering assets:\n" +
                "  - HearthStoneTiles material applied to Floor.prefab\n" +
                "  - HearthStonePad placed as a small settlement pad in World_Test\n" +
                "  - MeadowTerrain material and rolling mesh applied to World_Test/Ground\n" +
                "  - NavMeshSurface refreshed so Draugr can still path over the terrain";

            if (showDialogs)
            {
                EditorUtility.DisplayDialog("Steading Environment Art", message, "OK");
            }
            else
            {
                Debug.Log("[Steading] " + message);
            }
        }

        private static Texture2D CreateFloorTileTexture(string assetPath)
        {
            const int size = 512;
            const int tiles = 4;
            const int grout = 5;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var tileX = x / (size / tiles);
                    var tileY = y / (size / tiles);
                    var localX = x % (size / tiles);
                    var localY = y % (size / tiles);
                    var nearGrout = localX < grout || localY < grout || localX > (size / tiles) - grout || localY > (size / tiles) - grout;

                    var variation = ValueNoise(x, y, 41) * 0.14f + ValueNoise(x / 4, y / 4, 97) * 0.05f;
                    var tileVariation = ((tileX * 37 + tileY * 53) % 17) / 100f;
                    var edgeDistance = Mathf.Min(
                        Mathf.Min(localX, localY),
                        Mathf.Min((size / tiles) - localX, (size / tiles) - localY));
                    var edgeShade = edgeDistance / 18f;
                    edgeShade = Mathf.Clamp01(edgeShade);

                    var baseColor = Color.Lerp(
                        new Color(0.34f, 0.35f, 0.34f),
                        new Color(0.54f, 0.52f, 0.47f),
                        0.45f + variation + tileVariation);
                    baseColor *= Mathf.Lerp(0.75f, 1f, edgeShade);

                    if (nearGrout)
                    {
                        baseColor = Color.Lerp(baseColor, new Color(0.12f, 0.13f, 0.13f), 0.78f);
                    }
                    else if (IsCrackPixel(x, y))
                    {
                        baseColor = Color.Lerp(baseColor, new Color(0.08f, 0.08f, 0.08f), 0.55f);
                    }

                    pixels[y * size + x] = ToColor32(baseColor);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            SaveTexture(tex, assetPath, wrapRepeat: true);
            Object.DestroyImmediate(tex);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D CreateTerrainTexture(string assetPath)
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var broad = ValueNoise(x / 8, y / 8, 11);
                    var fine = ValueNoise(x, y, 19);
                    var path = Mathf.Abs(Mathf.Sin((x + y * 0.55f) * 0.018f));
                    var dirtBlend = Mathf.Clamp01((0.35f - path) * 2.6f + broad * 0.25f);

                    var grass = Color.Lerp(new Color(0.20f, 0.34f, 0.18f), new Color(0.42f, 0.48f, 0.25f), broad);
                    var dirt = Color.Lerp(new Color(0.27f, 0.22f, 0.16f), new Color(0.43f, 0.36f, 0.25f), fine);
                    var color = Color.Lerp(grass, dirt, dirtBlend * 0.55f);

                    color *= 0.92f + fine * 0.16f;
                    pixels[y * size + x] = ToColor32(color);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            SaveTexture(tex, assetPath, wrapRepeat: true);
            Object.DestroyImmediate(tex);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Mesh CreateTerrainMesh(string assetPath)
        {
            const int resolution = 65;
            const float size = 100f;
            var vertices = new Vector3[resolution * resolution];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    var i = z * resolution + x;
                    var px = (x / (float)(resolution - 1) - 0.5f) * size;
                    var pz = (z / (float)(resolution - 1) - 0.5f) * size;
                    var distFromCenter = new Vector2(px, pz).magnitude;
                    var flatten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7f, 20f, distFromCenter));
                    var height = (Mathf.Sin(px * 0.18f) + Mathf.Cos(pz * 0.16f) + Mathf.Sin((px + pz) * 0.08f)) * 0.38f * flatten;

                    vertices[i] = new Vector3(px, height, pz);
                    uvs[i] = new Vector2(x / 8f, z / 8f);
                }
            }

            var t = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    var i = z * resolution + x;
                    triangles[t++] = i;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + resolution + 1;
                }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "World_Test_Terrain" };
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Mesh CreateHearthPadMesh(string assetPath)
        {
            const int tilesWide = 8;
            const int tilesDeep = 8;
            const float tileSize = 2f;
            const float height = 0.04f;

            var width = tilesWide * tileSize;
            var depth = tilesDeep * tileSize;
            var vertices = new[]
            {
                new Vector3(-width * 0.5f, height, -depth * 0.5f),
                new Vector3(-width * 0.5f, height,  depth * 0.5f),
                new Vector3( width * 0.5f, height, -depth * 0.5f),
                new Vector3( width * 0.5f, height,  depth * 0.5f),
            };
            var uvs = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, tilesDeep),
                new Vector2(tilesWide, 0f),
                new Vector2(tilesWide, tilesDeep),
            };
            var triangles = new[] { 0, 1, 2, 2, 1, 3 };

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "HearthStonePad" };
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateLitMaterial(string assetPath, Texture2D texture, Color tint, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                var shader = FindOpaqueShader();
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }

            ApplyOpaqueSettings(mat);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Shader FindOpaqueShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static void ApplyOpaqueSettings(Material mat)
        {
            mat.shader = FindOpaqueShader();
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        private static void ApplyFloorMaterial(Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath) == null) return;

            using (var edit = new PrefabUtility.EditPrefabContentsScope(FloorPrefabPath))
            {
                foreach (var renderer in edit.prefabContentsRoot.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static void ApplyWorldTerrain(Mesh terrainMesh, Material terrainMaterial, Mesh hearthPadMesh, Material floorMaterial)
        {
            var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground");
            if (ground == null) ground = new GameObject("Ground");

            ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ground.transform.localScale = Vector3.one;

            var filter = ground.GetComponent<MeshFilter>() ?? ground.AddComponent<MeshFilter>();
            var renderer = ground.GetComponent<MeshRenderer>() ?? ground.AddComponent<MeshRenderer>();
            var collider = ground.GetComponent<MeshCollider>() ?? ground.AddComponent<MeshCollider>();

            filter.sharedMesh = terrainMesh;
            renderer.sharedMaterial = terrainMaterial;
            collider.sharedMesh = terrainMesh;

            var floor = GameObject.Find("HearthStonePad");
            if (floor == null) floor = new GameObject("HearthStonePad");
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = Vector3.one;

            var floorFilter = floor.GetComponent<MeshFilter>() ?? floor.AddComponent<MeshFilter>();
            var floorRenderer = floor.GetComponent<MeshRenderer>() ?? floor.AddComponent<MeshRenderer>();
            var floorCollider = floor.GetComponent<MeshCollider>() ?? floor.AddComponent<MeshCollider>();
            floorFilter.sharedMesh = hearthPadMesh;
            floorRenderer.sharedMaterial = floorMaterial;
            floorCollider.sharedMesh = hearthPadMesh;

            var surface = ground.GetComponent<NavMeshSurface>() ?? ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SaveTexture(Texture2D texture, string assetPath, bool wrapRepeat)
        {
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static void EnsureDir(string assetPath)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        }

        private static bool IsCrackPixel(int x, int y)
        {
            var crackA = Mathf.Abs((y - 90) - Mathf.Sin(x * 0.08f) * 12f - x * 0.12f) < 1.2f && x > 75 && x < 205;
            var crackB = Mathf.Abs((y - 345) + Mathf.Cos(x * 0.06f) * 10f - x * 0.05f) < 1.1f && x > 280 && x < 430;
            return crackA || crackB;
        }

        private static float ValueNoise(int x, int y, int seed)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263 + seed * 2147483647;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private static Color32 ToColor32(Color color)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                255);
        }
    }
}
