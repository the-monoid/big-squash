using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Steading.EditorTools
{
    // Generates the painterly post-process Volume + skybox material + scene
    // lighting that, together with Steading/PainterlyLit, give the world a
    // Valheim-leaning look. Idempotent — safe to re-run.
    public static class PainterlyAtmosphereSetup
    {
        private const string SettingsDir = "Assets/_Project/Settings";
        private const string ArtDir = "Assets/_Project/Art";
        private const string MaterialsDir = ArtDir + "/Materials";
        private const string VolumeProfilePath = SettingsDir + "/PainterlyVolumeProfile.asset";
        private const string SkyMaterialPath = MaterialsDir + "/PainterlySky.mat";
        private const string WorldScenePath = "Assets/_Project/Scenes/World_Test.unity";

        [MenuItem("Steading/Art: Apply Painterly Atmosphere (volume + sky + lights)")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Painterly Atmosphere",
                    "Cannot run while Play mode is active. Stop Play and try again.", "OK");
                return;
            }

            EnsureFolder(SettingsDir);
            EnsureFolder(MaterialsDir);

            var volumeProfile = CreateOrUpdateVolumeProfile();
            var skyMaterial   = CreateOrUpdateSkyMaterial();

            ApplyToWorldScene(volumeProfile, skyMaterial);
            ApplyRenderSettingsGlobal(skyMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Painterly Atmosphere",
                "Applied:\n" +
                "  • PainterlyVolumeProfile.asset (color grading + bloom + vignette + tonemap)\n" +
                "  • PainterlySky.mat (procedural gradient + sun)\n" +
                "  • World_Test scene: directional sun rotated to golden hour, fog enabled,\n" +
                "    cool ambient, post-process Volume in scene\n\n" +
                "Reopen Bootstrap and Play to see the change.",
                "OK");
        }

        // ---------------------------------------------------------- Volume profile

        private static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            ConfigureBloom(profile);
            ConfigureVignette(profile);
            ConfigureColorAdjustments(profile);
            ConfigureTonemapping(profile);
            ConfigureWhiteBalance(profile);
            ConfigureFilmGrain(profile);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T EnsureOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing)) return existing;
            return profile.Add<T>(true);
        }

        private static void ConfigureBloom(VolumeProfile profile)
        {
            var bloom = EnsureOverride<UnityEngine.Rendering.Universal.Bloom>(profile);
            bloom.active = true;
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.35f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 0.95f;
            bloom.scatter.overrideState = true; bloom.scatter.value = 0.78f;
            bloom.tint.overrideState = true; bloom.tint.value = new Color(1.0f, 0.95f, 0.85f);
            bloom.highQualityFiltering.overrideState = true; bloom.highQualityFiltering.value = true;
        }

        private static void ConfigureVignette(VolumeProfile profile)
        {
            var v = EnsureOverride<UnityEngine.Rendering.Universal.Vignette>(profile);
            v.active = true;
            v.intensity.overrideState = true; v.intensity.value = 0.28f;
            v.smoothness.overrideState = true; v.smoothness.value = 0.55f;
            v.color.overrideState = true; v.color.value = new Color(0.05f, 0.04f, 0.07f);
            v.rounded.overrideState = true; v.rounded.value = false;
        }

        private static void ConfigureColorAdjustments(VolumeProfile profile)
        {
            var c = EnsureOverride<UnityEngine.Rendering.Universal.ColorAdjustments>(profile);
            c.active = true;
            c.postExposure.overrideState = true; c.postExposure.value = 0.05f;
            c.contrast.overrideState = true; c.contrast.value = 14f;
            c.saturation.overrideState = true; c.saturation.value = 8f;
            c.colorFilter.overrideState = true; c.colorFilter.value = new Color(1.04f, 0.99f, 0.92f);
            c.hueShift.overrideState = true; c.hueShift.value = -2f;
        }

        private static void ConfigureTonemapping(VolumeProfile profile)
        {
            var t = EnsureOverride<UnityEngine.Rendering.Universal.Tonemapping>(profile);
            t.active = true;
            t.mode.overrideState = true; t.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.Neutral;
        }

        private static void ConfigureWhiteBalance(VolumeProfile profile)
        {
            var wb = EnsureOverride<UnityEngine.Rendering.Universal.WhiteBalance>(profile);
            wb.active = true;
            wb.temperature.overrideState = true; wb.temperature.value = 12f; // warm
            wb.tint.overrideState = true; wb.tint.value = -3f;
        }

        private static void ConfigureFilmGrain(VolumeProfile profile)
        {
            var f = EnsureOverride<UnityEngine.Rendering.Universal.FilmGrain>(profile);
            f.active = true;
            f.intensity.overrideState = true; f.intensity.value = 0.2f;
            f.response.overrideState = true; f.response.value = 0.8f;
        }

        // ---------------------------------------------------------- Sky material

        private static Material CreateOrUpdateSkyMaterial()
        {
            var shader = Shader.Find("Steading/PainterlySky");
            if (shader == null)
            {
                Debug.LogWarning("[Steading] PainterlySky shader not found. Falling back to default skybox.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "PainterlySky" };
                AssetDatabase.CreateAsset(mat, SkyMaterialPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_ZenithColor",  new Color(0.16f, 0.28f, 0.45f));
            mat.SetColor("_HorizonColor", new Color(0.78f, 0.62f, 0.45f));
            mat.SetColor("_GroundColor",  new Color(0.10f, 0.09f, 0.08f));
            mat.SetFloat("_HorizonPower", 3.6f);
            mat.SetFloat("_GroundPower",  6.0f);

            // Sun direction must match the scene Directional Light direction we set
            // in ApplyToWorldScene. Vector w = intensity.
            mat.SetVector("_SunDir", new Vector4(0.42f, 0.55f, 0.72f, 1.0f));
            mat.SetColor("_SunColor", new Color(1.20f, 1.04f, 0.70f));
            mat.SetFloat("_SunSize", 0.9988f);
            mat.SetFloat("_SunHaloSize", 0.92f);
            mat.SetFloat("_SunHaloIntensity", 0.6f);

            mat.SetFloat("_AtmoScatter", 0.55f);
            mat.SetColor("_CloudTint", new Color(1.05f, 1.0f, 0.95f));

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------- Scene wiring

        private static void ApplyToWorldScene(VolumeProfile profile, Material skyMaterial)
        {
            if (!File.Exists(WorldScenePath))
            {
                Debug.LogWarning("[Steading] World_Test.unity not found, skipping scene atmosphere.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

            // ---- Directional sun (rotated to golden hour, warm) ----
            var sun = FindOrCreateSun();
            sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.55f;
            light.color = new Color(1.0f, 0.93f, 0.75f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.92f;
            var addLight = sun.GetComponent<UniversalAdditionalLightData>();
            if (addLight == null) addLight = sun.AddComponent<UniversalAdditionalLightData>();

            // ---- Volume in scene ----
            var volumeGo = GameObject.Find("PainterlyVolume");
            if (volumeGo == null)
            {
                volumeGo = new GameObject("PainterlyVolume");
            }
            var volume = volumeGo.GetComponent<Volume>();
            if (volume == null) volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            // ---- Render settings on this scene ----
            if (skyMaterial != null) RenderSettings.skybox = skyMaterial;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor    = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.55f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.14f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.62f, 0.58f, 0.52f);

            RenderSettings.sun = light;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject FindOrCreateSun()
        {
            // Reuse any existing directional light named "Directional Light" or "Sun".
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    l.gameObject.name = "Sun";
                    return l.gameObject;
                }
            }
            var go = new GameObject("Sun");
            go.AddComponent<Light>().type = LightType.Directional;
            return go;
        }

        // ---------------------------------------------------------- Global render settings

        private static void ApplyRenderSettingsGlobal(Material skyMaterial)
        {
            // Editor-only: write a default ambient/sky fallback into GraphicsSettings so
            // newly-opened scenes inherit the painterly look without re-running this.
            if (skyMaterial != null)
            {
                var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (graphicsSettings != null && graphicsSettings.Length > 0)
                {
                    var so = new SerializedObject(graphicsSettings[0]);
                    var defaultSky = so.FindProperty("m_Skybox");
                    if (defaultSky != null && defaultSky.objectReferenceValue == null)
                    {
                        defaultSky.objectReferenceValue = skyMaterial;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
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
