using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Steading.Art;
using Steading.Combat;
using Steading.Player;

namespace Steading.World
{
    public static class EnvironmentRuntimeRenderer
    {
        private const string TerrainName = "ProceduralMeadows";
        private const string DecorRootName = "ProceduralMeadows_Decor";
        private const string HearthName = "HearthStonePad";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForInitialScene()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RenderEnvironment(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RenderEnvironment(scene);
        }

        private static void RenderEnvironment(Scene scene)
        {
            if (!scene.IsValid() || scene.name != "World_Test") return;

            DestroyIfPresent(TerrainName);
            DestroyIfPresent(DecorRootName);
            DestroyIfPresent(HearthName);

            HideGeneratedPlane();

            var terrain = new GameObject(TerrainName);
            terrain.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var mesh = CreateTerrainMesh();
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial = CreateTerrainMaterial();
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
            terrain.AddComponent<WalkableSurface>();

            CreateHearthPad();
            CreateDressing();
            TuneLighting();
        }

        private static void DestroyIfPresent(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.Destroy(existing);
        }

        private static void HideGeneratedPlane()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) return;

            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;

            var collider = ground.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        private static Mesh CreateTerrainMesh()
        {
            const int resolution = 129;
            const float size = 120f;
            var vertices = new Vector3[resolution * resolution];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    var px = (x / (float)(resolution - 1) - 0.5f) * size;
                    var pz = (z / (float)(resolution - 1) - 0.5f) * size;
                    var centerDistance = new Vector2(px, pz).magnitude;
                    var flatten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 26f, centerDistance));
                    var height = SampleTerrainHeight(px, pz) * flatten;

                    var i = z * resolution + x;
                    vertices[i] = new Vector3(px, height, pz);
                    uvs[i] = new Vector2(x / 10f, z / 10f);
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

            var mesh = new Mesh { name = "ProceduralMeadowsMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateTerrainMaterial()
        {
            var material = CreateOpaqueMaterial("MeadowsTerrainMaterial", Color.white, 0.12f);
            var texture = CreateTerrainTexture();

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            return material;
        }

        private static Texture2D CreateTerrainTexture()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "MeadowsTerrainTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var broad = SmoothNoise(x * 0.025f, y * 0.025f, 17);
                    var fine = SmoothNoise(x * 0.17f, y * 0.17f, 91);
                    var rock = Mathf.Pow(SmoothNoise(x * 0.055f + 40f, y * 0.055f, 33), 4f);
                    var path = Mathf.Abs(Mathf.Sin((x * 0.018f) + Mathf.Cos(y * 0.013f) * 1.8f));
                    var dirtBlend = Mathf.Clamp01((0.22f - path) * 3.2f + broad * 0.18f);

                    var grass = Color.Lerp(new Color(0.17f, 0.29f, 0.15f), new Color(0.47f, 0.50f, 0.25f), broad);
                    var moss = Color.Lerp(new Color(0.12f, 0.22f, 0.14f), new Color(0.30f, 0.37f, 0.19f), fine);
                    var dirt = Color.Lerp(new Color(0.24f, 0.19f, 0.13f), new Color(0.44f, 0.36f, 0.24f), fine);
                    var stone = Color.Lerp(new Color(0.28f, 0.30f, 0.28f), new Color(0.55f, 0.54f, 0.48f), fine);

                    var color = Color.Lerp(grass, moss, 0.25f);
                    color = Color.Lerp(color, dirt, dirtBlend * 0.65f);
                    color = Color.Lerp(color, stone, Mathf.Clamp01((rock - 0.55f) * 1.6f));
                    color *= 0.86f + fine * 0.22f;

                    pixels[y * size + x] = ToColor32(color);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static void CreateHearthPad()
        {
            var pad = new GameObject(HearthName);
            pad.transform.SetPositionAndRotation(new Vector3(0f, 0.07f, 0f), Quaternion.identity);

            var mesh = CreateStonePadMesh();
            pad.AddComponent<MeshFilter>().sharedMesh = mesh;
            pad.AddComponent<MeshRenderer>().sharedMaterial = CreateStoneMaterial("HearthStoneMaterial", 0.30f);
            pad.AddComponent<MeshCollider>().sharedMesh = mesh;
            pad.AddComponent<WalkableSurface>();

            AddRingStone(new Vector3(-3.2f, 0.1f, -3.2f), new Vector3(1.2f, 0.35f, 0.9f), 12f);
            AddRingStone(new Vector3(3.1f, 0.1f, -3.0f), new Vector3(1.0f, 0.35f, 1.1f), -18f);
            AddRingStone(new Vector3(-3.1f, 0.1f, 3.0f), new Vector3(1.1f, 0.32f, 1.0f), -35f);
            AddRingStone(new Vector3(3.3f, 0.1f, 3.1f), new Vector3(1.3f, 0.38f, 0.9f), 28f);
        }

        private static Mesh CreateStonePadMesh()
        {
            const int tilesWide = 7;
            const int tilesDeep = 7;
            const float tileSize = 1.35f;
            const float grout = 0.08f;

            var vertices = new Vector3[tilesWide * tilesDeep * 4];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[tilesWide * tilesDeep * 6];
            var vi = 0;
            var ti = 0;

            for (int z = 0; z < tilesDeep; z++)
            {
                for (int x = 0; x < tilesWide; x++)
                {
                    var cx = (x - (tilesWide - 1) * 0.5f) * tileSize;
                    var cz = (z - (tilesDeep - 1) * 0.5f) * tileSize;
                    var half = tileSize * 0.5f - grout;
                    var y = 0.01f + Hash01(x, z, 13) * 0.025f;

                    vertices[vi + 0] = new Vector3(cx - half, y, cz - half);
                    vertices[vi + 1] = new Vector3(cx - half, y, cz + half);
                    vertices[vi + 2] = new Vector3(cx + half, y, cz - half);
                    vertices[vi + 3] = new Vector3(cx + half, y, cz + half);
                    uvs[vi + 0] = new Vector2(0f, 0f);
                    uvs[vi + 1] = new Vector2(0f, 1f);
                    uvs[vi + 2] = new Vector2(1f, 0f);
                    uvs[vi + 3] = new Vector2(1f, 1f);

                    triangles[ti++] = vi;
                    triangles[ti++] = vi + 1;
                    triangles[ti++] = vi + 2;
                    triangles[ti++] = vi + 2;
                    triangles[ti++] = vi + 1;
                    triangles[ti++] = vi + 3;
                    vi += 4;
                }
            }

            var mesh = new Mesh { name = "HearthStonePadMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateDressing()
        {
            var root = new GameObject(DecorRootName);
            var bark = CreateFlatMaterial("BirchPineBark", new Color(0.27f, 0.19f, 0.12f), 0.2f);
            var leaf = CreateFlatMaterial("MeadowCanopy", new Color(0.18f, 0.31f, 0.16f), 0.18f);
            var grass = CreateFlatMaterial("GrassClump", new Color(0.38f, 0.48f, 0.21f), 0.05f);
            var stone = CreateStoneMaterial("FieldStone", 0.18f);
            var darkWood = CreateFlatMaterial("RaiderCampDarkWood", new Color(0.20f, 0.12f, 0.07f), 0.24f);
            var iron = CreateFlatMaterial("RaiderCampIron", new Color(0.15f, 0.16f, 0.15f), 0.38f);
            var banner = CreateFlatMaterial("RaiderCampBanner", new Color(0.36f, 0.055f, 0.045f), 0.50f);

            for (int i = 0; i < 28; i++)
            {
                var pos = SampleDecorPosition(i, 11, 14f, 56f);
                CreateTree(root.transform, pos, bark, leaf, i);
            }

            for (int i = 0; i < 42; i++)
            {
                var pos = SampleDecorPosition(i, 37, 8f, 58f);
                CreateRock(root.transform, pos, stone, i);
            }

            for (int i = 0; i < 160; i++)
            {
                var pos = SampleDecorPosition(i, 71, 5f, 58f);
                CreateGrassClump(root.transform, pos, grass, i);
            }

            for (int i = 0; i < 10; i++)
            {
                var pos = SampleDecorPosition(i, 123, 13f, 52f);
                CreateFallenLog(root.transform, pos, bark, i);
            }

            CreateRaiderCamps(root.transform, darkWood, stone, iron, banner);
        }

        private static void CreateRaiderCamps(Transform root, Material wood, Material stone, Material iron, Material banner)
        {
            for (int fortIndex = 0; fortIndex < EnemyFortLayout.Count; fortIndex++)
            {
                var center = EnemyFortLayout.GetCenter(fortIndex);
                center.y = SampleWorldHeight(center.x, center.z);

                var fort = new GameObject("RaiderCamp_" + fortIndex);
                fort.transform.SetParent(root, false);
                fort.transform.position = center;
                fort.transform.rotation = Quaternion.Euler(0f, 18f + fortIndex * 38f, 0f);

                var radius = 5.7f + fortIndex * 0.35f;
                var gateAngle = Mathf.Atan2(-center.z, -center.x);

                for (int i = 0; i < 34; i++)
                {
                    var angle = i / 34f * Mathf.PI * 2f;
                    if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, gateAngle * Mathf.Rad2Deg)) < 18f) continue;

                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var local = dir * radius;
                    var height = Mathf.Lerp(2.0f, 2.75f, Hash01(i, fortIndex, 91));
                    CreateFortPost(fort.transform, "PalisadePost_" + i, local, height, wood);

                    if (i % 3 == 0)
                    {
                        var brace = CreateFortBox(fort.transform, "WallBrace_" + i, local + Vector3.up * 1.10f, new Vector3(0.95f, 0.11f, 0.16f), wood);
                        brace.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
                    }
                }

                CreateGate(fort.transform, gateAngle, radius, wood, iron, banner);
                CreateWatchTower(fort.transform, Quaternion.Euler(0f, 45f, 0f) * Vector3.forward * (radius - 0.4f), wood, stone, iron);
                CreateWatchTower(fort.transform, Quaternion.Euler(0f, 212f, 0f) * Vector3.forward * (radius - 0.4f), wood, stone, iron);
                CreateTotem(fort.transform, Vector3.zero, wood, iron, banner);

                for (int i = 0; i < 9; i++)
                {
                    var angle = (i / 9f * Mathf.PI * 2f) + 0.21f;
                    var pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius + 1.25f);
                    CreateSpike(fort.transform, pos, angle, wood);
                }
            }
        }

        private static void CreateGate(Transform fort, float angle, float radius, Material wood, Material iron, Material banner)
        {
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            var left = dir * radius + side * 1.15f;
            var right = dir * radius - side * 1.15f;

            CreateFortPost(fort, "GateLeftPost", left, 3.1f, wood);
            CreateFortPost(fort, "GateRightPost", right, 3.1f, wood);
            var lintel = CreateFortBox(fort, "GateLintel", dir * radius + Vector3.up * 3.05f, new Vector3(2.65f, 0.22f, 0.26f), wood);
            lintel.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);

            var plate = CreateFortBox(fort, "GateIronPlate", dir * (radius - 0.08f) + Vector3.up * 2.14f, new Vector3(1.15f, 0.42f, 0.055f), iron);
            plate.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);

            var cloth = CreateFortBox(fort, "BloodBanner", dir * (radius - 0.12f) + Vector3.up * 1.42f, new Vector3(0.62f, 0.82f, 0.035f), banner);
            cloth.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
        }

        private static void CreateWatchTower(Transform fort, Vector3 localPosition, Material wood, Material stone, Material iron)
        {
            CreateFortBox(fort, "TowerStoneBase", localPosition + Vector3.up * 0.30f, new Vector3(1.45f, 0.60f, 1.45f), stone);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateFortPost(fort, "TowerPost", localPosition + new Vector3(x * 0.58f, 0f, z * 0.58f), 2.85f, wood);
                }
            }

            CreateFortBox(fort, "TowerPlatform", localPosition + Vector3.up * 2.15f, new Vector3(1.85f, 0.16f, 1.85f), wood);
            CreateFortBox(fort, "TowerRoof", localPosition + Vector3.up * 3.12f, new Vector3(1.70f, 0.22f, 1.70f), wood);
            CreateFortBox(fort, "TowerIronBoss", localPosition + new Vector3(0f, 2.48f, -0.92f), new Vector3(0.42f, 0.24f, 0.06f), iron);
        }

        private static void CreateTotem(Transform fort, Vector3 localPosition, Material wood, Material iron, Material banner)
        {
            CreateFortPost(fort, "BoneTotemPole", localPosition + Vector3.up * 0.1f, 3.25f, wood);
            CreateFortBox(fort, "TotemCross", localPosition + Vector3.up * 2.35f, new Vector3(1.35f, 0.12f, 0.12f), wood);
            CreateFortBox(fort, "TotemSkullPlate", localPosition + new Vector3(0f, 2.00f, -0.08f), new Vector3(0.42f, 0.36f, 0.08f), iron);
            CreateFortBox(fort, "TotemBannerA", localPosition + new Vector3(-0.28f, 1.36f, -0.05f), new Vector3(0.24f, 0.62f, 0.035f), banner);
            CreateFortBox(fort, "TotemBannerB", localPosition + new Vector3(0.28f, 1.22f, -0.05f), new Vector3(0.24f, 0.78f, 0.035f), banner);
        }

        private static void CreateSpike(Transform fort, Vector3 localPosition, float angle, Material wood)
        {
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spike.name = "OuterSpike";
            spike.transform.SetParent(fort, false);
            spike.transform.localPosition = localPosition + Vector3.up * 0.45f;
            spike.transform.localRotation = Quaternion.Euler(58f, -angle * Mathf.Rad2Deg, 0f);
            spike.transform.localScale = new Vector3(0.10f, 0.75f, 0.10f);
            spike.GetComponent<Renderer>().sharedMaterial = wood;
        }

        private static void CreateFortPost(Transform fort, string name, Vector3 localPosition, float height, Material material)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = name;
            post.transform.SetParent(fort, false);
            post.transform.localPosition = localPosition + Vector3.up * (height * 0.5f);
            post.transform.localScale = new Vector3(0.18f, height * 0.5f, 0.18f);
            post.GetComponent<Renderer>().sharedMaterial = material;
            AddCarvingObstacle(post, new Vector3(0.34f, height, 0.34f));
        }

        private static GameObject CreateFortBox(Transform fort, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var box = new GameObject(name);
            box.name = name;
            box.transform.SetParent(fort, false);
            box.transform.localPosition = localPosition;
            box.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateRoughBoxMesh(name + "Mesh", scale, ProceduralArt.StableSeed(fort.name + ":" + name), Mathf.Min(0.035f, scale.magnitude * 0.012f), 3);
            var renderer = box.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            var collider = box.AddComponent<BoxCollider>();
            collider.size = scale;
            AddCarvingObstacle(box, scale);
            return box;
        }

        private static void AddCarvingObstacle(GameObject go, Vector3 size)
        {
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = size;
            obstacle.carving = true;
        }

        private static Vector3 SampleDecorPosition(int index, int seed, float minRadius, float maxRadius)
        {
            var angle = Hash01(index, seed, 1) * Mathf.PI * 2f;
            var radius = Mathf.Lerp(minRadius, maxRadius, Hash01(index, seed, 2));
            var x = Mathf.Cos(angle) * radius + (Hash01(index, seed, 3) - 0.5f) * 8f;
            var z = Mathf.Sin(angle) * radius + (Hash01(index, seed, 4) - 0.5f) * 8f;
            return new Vector3(x, SampleWorldHeight(x, z), z);
        }

        private static void CreateTree(Transform root, Vector3 pos, Material bark, Material leaf, int index)
        {
            var tree = new GameObject("Pine_" + index);
            tree.transform.SetParent(root, false);
            tree.transform.position = pos;
            tree.transform.rotation = Quaternion.Euler(0f, Hash01(index, 5, 8) * 360f, 0f);
            tree.AddComponent<ResourceNode>().Configure(
                "meadows_tree_" + index,
                ResourceKind.Wood,
                Mathf.RoundToInt(Mathf.Lerp(45f, 80f, Hash01(index, 5, 10))),
                Mathf.RoundToInt(Mathf.Lerp(5f, 10f, Hash01(index, 5, 11))),
                WeaponKind.Axe);

            var height = Mathf.Lerp(5.5f, 9.5f, Hash01(index, 5, 9));
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(0.34f, height * 0.5f, 0.34f);
            trunk.GetComponent<Renderer>().sharedMaterial = bark;

            for (int i = 0; i < 4; i++)
            {
                var canopy = new GameObject("PineNeedleLayer_" + i);
                canopy.transform.SetParent(tree.transform, false);
                canopy.transform.localPosition = new Vector3(0f, height * (0.42f + i * 0.13f), 0f);
                canopy.transform.localRotation = Quaternion.Euler(0f, i * 28f + Hash01(index, i, 15) * 18f, 0f);
                var radius = Mathf.Lerp(1.75f, 0.58f, i / 3f) * Mathf.Lerp(0.86f, 1.18f, Hash01(index, i, 16));
                var layerHeight = Mathf.Lerp(1.55f, 0.80f, i / 3f);
                canopy.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateConeMesh("PineNeedleLayerMesh", radius, layerHeight, index * 17 + i, 18, 0.18f);
                var renderer = canopy.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = leaf;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void CreateRock(Transform root, Vector3 pos, Material material, int index)
        {
            var rock = new GameObject("FieldStone_" + index);
            rock.name = "FieldStone_" + index;
            rock.transform.SetParent(root, false);
            rock.transform.position = pos + Vector3.up * 0.2f;
            rock.transform.rotation = Quaternion.Euler(Hash01(index, 8, 1) * 25f, Hash01(index, 8, 2) * 360f, Hash01(index, 8, 3) * 20f);
            var scale = new Vector3(
                Mathf.Lerp(0.7f, 2.3f, Hash01(index, 8, 4)),
                Mathf.Lerp(0.25f, 0.9f, Hash01(index, 8, 5)),
                Mathf.Lerp(0.6f, 2.0f, Hash01(index, 8, 6)));
            rock.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateRockMesh("FieldStoneMesh", scale * 0.5f, index + 80, 8, 16, 0.34f);
            var renderer = rock.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void CreateGrassClump(Transform root, Vector3 pos, Material material, int index)
        {
            var clump = new GameObject("Grass_" + index);
            clump.transform.SetParent(root, false);
            clump.transform.position = pos + Vector3.up * 0.04f;
            clump.transform.rotation = Quaternion.Euler(0f, Hash01(index, 9, 1) * 360f, 0f);

            for (int i = 0; i < 3; i++)
            {
                var blade = new GameObject("Blade");
                blade.name = "Blade";
                blade.transform.SetParent(clump.transform, false);
                blade.transform.localRotation = Quaternion.Euler(
                    Mathf.Lerp(-8f, 8f, Hash01(index, i, 6)),
                    i * 60f,
                    Mathf.Lerp(-10f, 10f, Hash01(index, i, 7)));
                var bladeHeight = Mathf.Lerp(0.55f, 1.1f, Hash01(index, i, 4));
                blade.transform.localPosition = Vector3.zero;
                blade.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateGrassBladeMesh("GrassBladeMesh", 0.16f, bladeHeight, Mathf.Lerp(-0.22f, 0.22f, Hash01(index, i, 5)), index * 13 + i);
                var renderer = blade.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        private static void CreateFallenLog(Transform root, Vector3 pos, Material bark, int index)
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "FallenLog_" + index;
            log.transform.SetParent(root, false);
            log.transform.position = pos + Vector3.up * 0.28f;
            log.transform.rotation = Quaternion.Euler(90f, Hash01(index, 10, 1) * 360f, 0f);
            log.transform.localScale = new Vector3(0.32f, Mathf.Lerp(1.8f, 3.4f, Hash01(index, 10, 2)), 0.32f);
            log.GetComponent<Renderer>().sharedMaterial = bark;
        }

        private static void AddRingStone(Vector3 pos, Vector3 scale, float yaw)
        {
            var stone = new GameObject("HearthEdgeStone");
            stone.name = "HearthEdgeStone";
            stone.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            stone.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateRockMesh("HearthEdgeStoneMesh", scale * 0.5f, Mathf.RoundToInt(yaw * 10f) + 19, 7, 14, 0.28f);
            var renderer = stone.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateStoneMaterial("HearthEdgeStoneMaterial", 0.24f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static Material CreateFlatMaterial(string name, Color color, float smoothness)
        {
            return ProceduralArt.CreateLitMaterial(name, color, SurfaceForMaterial(name), smoothness, 0f);
        }

        private static Material CreateStoneMaterial(string name, float smoothness)
        {
            return ProceduralArt.CreateLitMaterial(name, new Color(0.42f, 0.42f, 0.38f), ArtSurface.Stone, smoothness, 0f);
        }

        private static ArtSurface SurfaceForMaterial(string name)
        {
            if (name.Contains("Bark") || name.Contains("Wood")) return ArtSurface.Bark;
            if (name.Contains("Canopy") || name.Contains("Leaf")) return ArtSurface.Leaves;
            if (name.Contains("Grass")) return ArtSurface.Grass;
            if (name.Contains("Stone")) return ArtSurface.Stone;
            if (name.Contains("Iron")) return ArtSurface.DarkMetal;
            if (name.Contains("Banner")) return ArtSurface.Banner;
            return ArtSurface.Plain;
        }

        private static Material CreateOpaqueMaterial(string name, Color color, float smoothness)
        {
            var material = new Material(FindRenderableShader()) { name = name };
            ApplyOpaqueMaterialSettings(material, color, smoothness);
            return material;
        }

        private static Shader FindRenderableShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static void ApplyOpaqueMaterialSettings(Material material, Color color, float smoothness)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        private static Texture2D CreateStoneTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "StoneMottle",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var n = SmoothNoise(x * 0.06f, y * 0.06f, 51) * 0.6f + SmoothNoise(x * 0.22f, y * 0.22f, 52) * 0.4f;
                    var color = Color.Lerp(new Color(0.25f, 0.26f, 0.25f), new Color(0.62f, 0.60f, 0.54f), n);
                    pixels[y * size + x] = ToColor32(color);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static void TuneLighting()
        {
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.40f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.60f, 0.58f);
            RenderSettings.fogDensity = 0.012f;

            var light = GameObject.Find("Directional Light");
            if (light == null) return;

            light.transform.rotation = Quaternion.Euler(45f, -32f, 0f);
            var directional = light.GetComponent<Light>();
            if (directional != null)
            {
                directional.color = new Color(1f, 0.91f, 0.78f);
                directional.intensity = 1.25f;
            }
        }

        private static float SampleWorldHeight(float x, float z)
        {
            var centerDistance = new Vector2(x, z).magnitude;
            var flatten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 26f, centerDistance));
            return SampleTerrainHeight(x, z) * flatten;
        }

        private static float SampleTerrainHeight(float x, float z)
        {
            var hills = SmoothNoise(x * 0.035f, z * 0.035f, 5) * 2f - 1f;
            var ridges = Mathf.Abs(SmoothNoise(x * 0.085f + 20f, z * 0.085f - 12f, 8) * 2f - 1f);
            var undulation = Mathf.Sin(x * 0.11f + z * 0.03f) * 0.35f + Mathf.Cos(z * 0.10f) * 0.25f;
            return hills * 1.6f + ridges * 0.9f + undulation;
        }

        private static float SmoothNoise(float x, float y, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = Mathf.SmoothStep(0f, 1f, x - x0);
            var ty = Mathf.SmoothStep(0f, 1f, y - y0);

            var a = Hash01(x0, y0, seed);
            var b = Hash01(x0 + 1, y0, seed);
            var c = Hash01(x0, y0 + 1, seed);
            var d = Hash01(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263 + seed * 1442695041;
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
