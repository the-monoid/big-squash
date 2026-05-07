using System.Collections.Generic;
using UnityEngine;

namespace Steading.Art
{
    public enum ArtSurface
    {
        Plain,
        Skin,
        Cloth,
        Wool,
        Leather,
        Wood,
        Bark,
        Fur,
        Hair,
        Metal,
        DarkMetal,
        Stone,
        Grass,
        Leaves,
        Dirt,
        Bone,
        EyeGlow,
        Banner,
    }

    public static class ProceduralArt
    {
        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        public static Material CreateLitMaterial(string name, Color color, ArtSurface surface, float smoothness, float metallic, int seed = 0)
        {
            var material = new Material(FindRenderableShader()) { name = name };
            ApplyOpaqueMaterialSettings(material, color, smoothness, metallic);
            ApplyPainterlyTuning(material, surface, color);

            var texture = GetSurfaceTexture(name, Color.white, surface, seed == 0 ? StableSeed(name) : seed);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);

            if (surface == ArtSurface.EyeGlow)
            {
                var glow = color * 2.1f;
                glow.a = 1f;
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", glow);
                material.EnableKeyword("_EMISSION");
            }

            return material;
        }

        // Painterly per-surface knob tuning. The Steading/PainterlyLit shader exposes
        // _ShadowTint / _MidtoneTint / _HighlightTint / _RimColor / _RimIntensity. Each
        // surface type gets a thoughtful palette so the same shader reads as cloth or
        // metal or grass at a glance, instead of one flat banded look across everything.
        private static void ApplyPainterlyTuning(Material material, ArtSurface surface, Color baseColor)
        {
            if (!material.HasProperty("_ShadowTint")) return; // not the painterly shader

            // Defaults
            var shadowTint   = new Color(0.36f, 0.42f, 0.55f);
            var midtoneTint  = new Color(0.85f, 0.82f, 0.78f);
            var highlight    = new Color(1.05f, 1.00f, 0.92f);
            var rimColor     = new Color(1.10f, 0.85f, 0.50f);
            var rimIntensity = 0.55f;
            var rimPower     = 3.6f;
            var ambient      = 0.55f;

            switch (surface)
            {
                case ArtSurface.Skin:
                    shadowTint   = new Color(0.55f, 0.32f, 0.38f); // warm shadow (sub-surface fake)
                    midtoneTint  = new Color(0.92f, 0.82f, 0.74f);
                    highlight    = new Color(1.05f, 0.98f, 0.88f);
                    rimColor     = new Color(1.20f, 0.80f, 0.55f);
                    rimIntensity = 0.50f;
                    break;
                case ArtSurface.Cloth:
                case ArtSurface.Wool:
                    shadowTint   = new Color(0.30f, 0.32f, 0.42f);
                    rimIntensity = 0.30f;
                    rimPower     = 4.5f;
                    break;
                case ArtSurface.Leather:
                    shadowTint   = new Color(0.28f, 0.22f, 0.20f);
                    rimColor     = new Color(0.95f, 0.78f, 0.55f);
                    rimIntensity = 0.40f;
                    break;
                case ArtSurface.Wood:
                case ArtSurface.Bark:
                    shadowTint   = new Color(0.26f, 0.22f, 0.18f);
                    midtoneTint  = new Color(0.78f, 0.70f, 0.58f);
                    rimColor     = new Color(1.00f, 0.78f, 0.50f);
                    rimIntensity = 0.35f;
                    break;
                case ArtSurface.Hair:
                case ArtSurface.Fur:
                    shadowTint   = new Color(0.20f, 0.20f, 0.25f);
                    rimColor     = new Color(1.20f, 0.95f, 0.65f);
                    rimIntensity = 0.85f;   // hair pops with strong rim
                    rimPower     = 2.4f;
                    break;
                case ArtSurface.Metal:
                    shadowTint   = new Color(0.22f, 0.26f, 0.34f);
                    highlight    = new Color(1.30f, 1.20f, 1.00f);
                    rimColor     = new Color(1.40f, 1.20f, 0.85f);
                    rimIntensity = 1.10f;
                    rimPower     = 2.0f;
                    break;
                case ArtSurface.DarkMetal:
                    shadowTint   = new Color(0.10f, 0.12f, 0.16f);
                    highlight    = new Color(0.95f, 0.95f, 1.00f);
                    rimColor     = new Color(1.10f, 1.00f, 0.85f);
                    rimIntensity = 0.85f;
                    break;
                case ArtSurface.Stone:
                    shadowTint   = new Color(0.30f, 0.34f, 0.42f);
                    midtoneTint  = new Color(0.78f, 0.78f, 0.80f);
                    rimIntensity = 0.25f;
                    rimPower     = 5.5f;
                    break;
                case ArtSurface.Grass:
                case ArtSurface.Leaves:
                    shadowTint   = new Color(0.18f, 0.32f, 0.22f);
                    midtoneTint  = new Color(0.62f, 0.78f, 0.45f);
                    highlight    = new Color(0.95f, 1.05f, 0.65f);
                    rimColor     = new Color(0.85f, 1.00f, 0.55f);
                    rimIntensity = 0.65f;
                    break;
                case ArtSurface.Dirt:
                    shadowTint   = new Color(0.20f, 0.16f, 0.12f);
                    midtoneTint  = new Color(0.70f, 0.58f, 0.42f);
                    rimIntensity = 0.20f;
                    break;
                case ArtSurface.Bone:
                    shadowTint   = new Color(0.50f, 0.45f, 0.40f);
                    midtoneTint  = new Color(0.92f, 0.88f, 0.80f);
                    rimColor     = new Color(1.05f, 0.95f, 0.80f);
                    rimIntensity = 0.45f;
                    break;
                case ArtSurface.EyeGlow:
                    shadowTint   = baseColor * 0.6f;
                    midtoneTint  = baseColor * 1.2f;
                    highlight    = baseColor * 2.0f;
                    rimColor     = baseColor;
                    rimIntensity = 1.5f;
                    ambient      = 0.2f;
                    break;
                case ArtSurface.Banner:
                    rimColor     = new Color(1.10f, 1.00f, 0.85f);
                    rimIntensity = 0.45f;
                    break;
            }

            material.SetColor("_ShadowTint",   shadowTint);
            material.SetColor("_MidtoneTint",  midtoneTint);
            material.SetColor("_HighlightTint", highlight);
            material.SetColor("_RimColor",     rimColor);
            material.SetFloat("_RimIntensity", rimIntensity);
            material.SetFloat("_RimPower",     rimPower);
            material.SetFloat("_AmbientStrength", ambient);
        }

        public static Mesh CreateRoughBoxMesh(string name, Vector3 size, int seed, float wobble = 0.018f, int subdivisions = 3)
        {
            var hx = Mathf.Abs(size.x) * 0.5f;
            var hy = Mathf.Abs(size.y) * 0.5f;
            var hz = Mathf.Abs(size.z) * 0.5f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var s = Mathf.Max(1, subdivisions);

            AddBoxFace(vertices, triangles, uvs, new Vector3(0f, 0f, hz), new Vector3(size.x, 0f, 0f), new Vector3(0f, size.y, 0f), Vector3.forward, s, seed + 11, wobble);
            AddBoxFace(vertices, triangles, uvs, new Vector3(0f, 0f, -hz), new Vector3(-size.x, 0f, 0f), new Vector3(0f, size.y, 0f), Vector3.back, s, seed + 17, wobble);
            AddBoxFace(vertices, triangles, uvs, new Vector3(hx, 0f, 0f), new Vector3(0f, 0f, -size.z), new Vector3(0f, size.y, 0f), Vector3.right, s, seed + 23, wobble);
            AddBoxFace(vertices, triangles, uvs, new Vector3(-hx, 0f, 0f), new Vector3(0f, 0f, size.z), new Vector3(0f, size.y, 0f), Vector3.left, s, seed + 29, wobble);
            AddBoxFace(vertices, triangles, uvs, new Vector3(0f, hy, 0f), new Vector3(size.x, 0f, 0f), new Vector3(0f, 0f, -size.z), Vector3.up, s, seed + 31, wobble);
            AddBoxFace(vertices, triangles, uvs, new Vector3(0f, -hy, 0f), new Vector3(size.x, 0f, 0f), new Vector3(0f, 0f, size.z), Vector3.down, s, seed + 37, wobble);

            return FinishMesh(name, vertices, triangles, uvs);
        }

        public static Mesh CreateRockMesh(string name, Vector3 radius, int seed, int latitude = 8, int longitude = 16, float roughness = 0.22f)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var lat = Mathf.Max(4, latitude);
            var lon = Mathf.Max(8, longitude);

            for (int y = 0; y <= lat; y++)
            {
                var v = y / (float)lat;
                var phi = v * Mathf.PI;
                var sin = Mathf.Sin(phi);
                var cos = Mathf.Cos(phi);
                for (int x = 0; x <= lon; x++)
                {
                    var u = x / (float)lon;
                    var theta = u * Mathf.PI * 2f;
                    var n = SmoothNoise(u * 8f + seed, v * 8f, seed) * 0.6f + SmoothNoise(u * 19f, v * 19f + seed, seed + 9) * 0.4f;
                    var lobe = 1f + (n - 0.5f) * roughness;
                    vertices.Add(new Vector3(Mathf.Cos(theta) * sin * radius.x * lobe, cos * radius.y * lobe, Mathf.Sin(theta) * sin * radius.z * lobe));
                    uvs.Add(new Vector2(u, v));
                }
            }

            var row = lon + 1;
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    var a = y * row + x;
                    var b = a + row;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            return FinishMesh(name, vertices, triangles, uvs);
        }

        public static Mesh CreateConeMesh(string name, float radius, float height, int seed, int segments = 18, float unevenness = 0.08f)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var count = Mathf.Max(6, segments);

            for (int i = 0; i < count; i++)
            {
                var t = i / (float)count;
                var angle = t * Mathf.PI * 2f;
                var n = Mathf.Lerp(1f - unevenness, 1f + unevenness, Hash01(i, seed, 19));
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius * n, 0f, Mathf.Sin(angle) * radius * n));
                uvs.Add(new Vector2(t, 0f));
            }

            var tip = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f));
            uvs.Add(new Vector2(0.5f, 1f));
            var baseCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < count; i++)
            {
                var ni = (i + 1) % count;
                triangles.Add(i);
                triangles.Add(tip);
                triangles.Add(ni);
                triangles.Add(baseCenter);
                triangles.Add(ni);
                triangles.Add(i);
            }

            return FinishMesh(name, vertices, triangles, uvs);
        }

        public static Mesh CreateGrassBladeMesh(string name, float width, float height, float bend, int seed)
        {
            var sway = (Hash01(seed, 8, 2) - 0.5f) * width;
            var vertices = new List<Vector3>
            {
                new Vector3(-width * 0.5f, 0f, 0f),
                new Vector3(width * 0.5f, 0f, 0f),
                new Vector3(width * 0.32f + sway * 0.35f, height * 0.52f, bend * 0.35f),
                new Vector3(-width * 0.32f + sway * 0.15f, height * 0.52f, bend * 0.25f),
                new Vector3(sway, height, bend),
                new Vector3(-width * 0.5f, 0f, 0f),
                new Vector3(width * 0.5f, 0f, 0f),
                new Vector3(width * 0.32f + sway * 0.35f, height * 0.52f, bend * 0.35f),
                new Vector3(-width * 0.32f + sway * 0.15f, height * 0.52f, bend * 0.25f),
                new Vector3(sway, height, bend),
            };
            var triangles = new List<int>
            {
                0, 1, 2,
                0, 2, 3,
                3, 2, 4,
                6, 5, 7,
                7, 5, 8,
                7, 8, 9,
            };
            var uvs = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.78f, 0.55f),
                new Vector2(0.22f, 0.55f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.78f, 0.55f),
                new Vector2(0.22f, 0.55f),
                new Vector2(0.5f, 1f),
            };
            return FinishMesh(name, vertices, triangles, uvs);
        }

        public static int StableSeed(string text)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private static Texture2D GetSurfaceTexture(string name, Color baseColor, ArtSurface surface, int seed)
        {
            var key = surface + ":" + seed + ":" + ColorUtility.ToHtmlStringRGBA(baseColor);
            if (TextureCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var size = surface == ArtSurface.Grass || surface == ArtSurface.Leaves || surface == ArtSurface.Bark ? 256 : 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = name + "_ProceduralTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var u = x / (float)size;
                    var v = y / (float)size;
                    var broad = SmoothNoise(u * 6f + seed * 0.013f, v * 6f, seed);
                    var fine = SmoothNoise(u * 32f, v * 32f + seed * 0.017f, seed + 41);
                    var color = SurfaceColor(baseColor, surface, u, v, broad, fine, seed);
                    pixels[y * size + x] = ToColor32(color);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            TextureCache[key] = texture;
            return texture;
        }

        private static Color SurfaceColor(Color baseColor, ArtSurface surface, float u, float v, float broad, float fine, int seed)
        {
            var color = baseColor;
            switch (surface)
            {
                case ArtSurface.Skin:
                    color = Multiply(Color.Lerp(Multiply(baseColor, 0.78f), Multiply(baseColor, 1.08f), broad * 0.36f + fine * 0.18f), 0.92f);
                    if (SmoothNoise(u * 80f, v * 80f, seed + 6) > 0.82f) color = Color.Lerp(color, Multiply(baseColor, 0.42f), 0.16f);
                    break;
                case ArtSurface.Cloth:
                case ArtSurface.Wool:
                case ArtSurface.Banner:
                    var weave = (Mathf.Sin(u * 120f) * 0.5f + 0.5f) * 0.08f + (Mathf.Sin(v * 96f) * 0.5f + 0.5f) * 0.10f;
                    var worn = surface == ArtSurface.Banner ? 0.62f + broad * 0.28f : 0.74f + broad * 0.22f;
                    color = Multiply(baseColor, worn + weave + fine * 0.08f);
                    break;
                case ArtSurface.Leather:
                    var pores = Mathf.Pow(fine, 3f) * 0.22f;
                    color = Color.Lerp(Multiply(baseColor, 0.62f + broad * 0.26f), new Color(0.11f, 0.055f, 0.030f), pores);
                    break;
                case ArtSurface.Wood:
                    var grain = Mathf.Sin((u * 30f) + broad * 6f + seed * 0.01f) * 0.5f + 0.5f;
                    var ring = Mathf.Pow(1f - Mathf.Abs(grain - 0.5f) * 2f, 2f);
                    color = Color.Lerp(Multiply(baseColor, 0.62f), Multiply(baseColor, 1.28f), ring * 0.55f + fine * 0.20f);
                    if (SmoothNoise(u * 9f, v * 22f, seed + 10) > 0.76f) color = Color.Lerp(color, new Color(0.13f, 0.065f, 0.025f), 0.32f);
                    break;
                case ArtSurface.Bark:
                    var ridges = Mathf.Sin(u * 84f + broad * 9f) * 0.5f + 0.5f;
                    color = Color.Lerp(Multiply(baseColor, 0.48f), Multiply(baseColor, 1.12f), ridges * 0.55f + fine * 0.20f);
                    if (fine > 0.78f) color = Color.Lerp(color, new Color(0.08f, 0.050f, 0.025f), 0.32f);
                    break;
                case ArtSurface.Fur:
                case ArtSurface.Hair:
                    var strands = Mathf.Sin((u + v * 0.22f) * 120f + broad * 7f) * 0.5f + 0.5f;
                    color = Multiply(baseColor, 0.58f + strands * 0.34f + fine * 0.16f);
                    break;
                case ArtSurface.Metal:
                case ArtSurface.DarkMetal:
                    var scratch = Mathf.Sin((u + v * 0.18f) * 220f + fine * 4f) * 0.5f + 0.5f;
                    color = Multiply(baseColor, 0.78f + broad * 0.16f + scratch * 0.11f);
                    if (surface == ArtSurface.DarkMetal) color = Color.Lerp(color, new Color(0.10f, 0.105f, 0.10f), 0.18f);
                    break;
                case ArtSurface.Stone:
                    var chip = SmoothNoise(u * 18f + 9f, v * 18f - 4f, seed + 2);
                    color = Color.Lerp(Multiply(baseColor, 0.56f), Multiply(baseColor, 1.34f), broad * 0.52f + fine * 0.28f + chip * 0.20f);
                    break;
                case ArtSurface.Grass:
                    var blade = Mathf.Sin((u * 95f) + broad * 8f) * 0.5f + 0.5f;
                    color = Color.Lerp(Multiply(baseColor, 0.42f), Multiply(baseColor, 1.34f), broad * 0.50f + blade * 0.26f + fine * 0.14f);
                    break;
                case ArtSurface.Leaves:
                    var needle = Mathf.Sin((u + v * 0.65f) * 110f) * 0.5f + 0.5f;
                    color = Color.Lerp(Multiply(baseColor, 0.38f), Multiply(baseColor, 1.22f), broad * 0.42f + needle * 0.26f + fine * 0.16f);
                    break;
                case ArtSurface.Dirt:
                    color = Color.Lerp(Multiply(baseColor, 0.50f), Multiply(baseColor, 1.25f), broad * 0.62f + fine * 0.18f);
                    break;
                case ArtSurface.Bone:
                    color = Color.Lerp(Multiply(baseColor, 0.68f), Multiply(baseColor, 1.20f), broad * 0.55f + fine * 0.20f);
                    break;
                case ArtSurface.EyeGlow:
                    color = Color.Lerp(baseColor, Color.white, Mathf.Pow(fine, 3f) * 0.32f);
                    break;
                default:
                    color = Multiply(baseColor, 0.80f + broad * 0.24f + fine * 0.08f);
                    break;
            }

            color.a = 1f;
            return color;
        }

        private static void AddBoxFace(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 center, Vector3 axisU, Vector3 axisV, Vector3 normal, int subdivisions, int seed, float wobble)
        {
            var start = vertices.Count;
            var s = Mathf.Max(1, subdivisions);
            for (int y = 0; y <= s; y++)
            {
                var v = y / (float)s;
                for (int x = 0; x <= s; x++)
                {
                    var u = x / (float)s;
                    var p = center + axisU * (u - 0.5f) + axisV * (v - 0.5f);
                    var edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    var edgeMask = Mathf.SmoothStep(0f, 0.34f, edge);
                    var n = SmoothNoise(p.x * 7f + seed, p.y * 7f - seed * 0.13f, seed + Mathf.RoundToInt(p.z * 19f));
                    p += normal * ((n - 0.5f) * wobble * edgeMask);
                    vertices.Add(p);
                    uvs.Add(new Vector2(u, v));
                }
            }

            var row = s + 1;
            var outward = Vector3.Dot(Vector3.Cross(axisU, axisV), normal) >= 0f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    var a = start + y * row + x;
                    var b = a + row;
                    if (outward)
                    {
                        triangles.Add(a);
                        triangles.Add(a + 1);
                        triangles.Add(b);
                        triangles.Add(a + 1);
                        triangles.Add(b + 1);
                        triangles.Add(b);
                    }
                    else
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(a + 1);
                        triangles.Add(a + 1);
                        triangles.Add(b);
                        triangles.Add(b + 1);
                    }
                }
            }
        }

        private static Mesh FinishMesh(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (uvs != null && uvs.Count == vertices.Count) mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material ApplyOpaqueMaterialSettings(Material material, Color color, float smoothness, float metallic)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            return material;
        }

        private static Shader FindRenderableShader()
        {
            // Prefer the painterly shader so all procedural materials inherit the
            // banded lighting + rim look. Fall back gracefully if the project hasn't
            // imported the .shader file yet.
            return Shader.Find("Steading/PainterlyLit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static Color Multiply(Color color, float amount)
        {
            return new Color(Mathf.Clamp01(color.r * amount), Mathf.Clamp01(color.g * amount), Mathf.Clamp01(color.b * amount), color.a);
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
