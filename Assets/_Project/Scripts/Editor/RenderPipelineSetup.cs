using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Steading.EditorTools
{
    [InitializeOnLoad]
    public static class RenderPipelineSetup
    {
        private const string SettingsDir = "Assets/_Project/Settings";
        private const string PipelinePath = SettingsDir + "/SteadingURP.asset";
        private const string AutoConfiguredKey = "Steading.RenderPipelineSetup.AutoConfigured";

        static RenderPipelineSetup()
        {
            EditorApplication.delayCall += EnsureConfiguredOnce;
        }

        [MenuItem("Steading/Art: Repair URP Render Pipeline")]
        public static void ConfigureNow()
        {
            ConfigurePipeline();
            Debug.Log("[Steading] URP render pipeline assigned. Pink URP materials should render normally after the scene refreshes.");
        }

        private static void EnsureConfiguredOnce()
        {
            if (SessionState.GetBool(AutoConfiguredKey, false)) return;
            SessionState.SetBool(AutoConfiguredKey, true);

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
            {
                RepairQualitySettings(GraphicsSettings.defaultRenderPipeline);
                return;
            }

            ConfigurePipeline();
        }

        private static void ConfigurePipeline()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder(SettingsDir);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create();
                pipeline.name = "SteadingURP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                SaveRendererDataAsSubAsset(pipeline);
            }
            else
            {
                EnsureRendererDataExists(pipeline);
                EditorUtility.SetDirty(pipeline);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            RepairQualitySettings(pipeline);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureRendererDataExists(UniversalRenderPipelineAsset pipeline)
        {
            var so = new SerializedObject(pipeline);
            var rendererListProp = so.FindProperty("m_RendererDataList");
            if (rendererListProp == null) return;

            if (rendererListProp.arraySize < 1) rendererListProp.arraySize = 1;
            var rendererElement = rendererListProp.GetArrayElementAtIndex(0);
            if (rendererElement.objectReferenceValue != null) return;

            var repaired = UniversalRenderPipelineAsset.Create();
            var repairedSo = new SerializedObject(repaired);
            var repairedRendererList = repairedSo.FindProperty("m_RendererDataList");
            var rendererData = repairedRendererList?.GetArrayElementAtIndex(0).objectReferenceValue;

            if (rendererData != null)
            {
                rendererElement.objectReferenceValue = rendererData;
                var rendererDataProp = so.FindProperty("m_RendererData");
                if (rendererDataProp != null) rendererDataProp.objectReferenceValue = rendererData;
                so.ApplyModifiedPropertiesWithoutUndo();
                SaveRendererDataAsSubAsset(pipeline);
            }

            Object.DestroyImmediate(repaired);
        }

        private static void SaveRendererDataAsSubAsset(UniversalRenderPipelineAsset pipeline)
        {
            var so = new SerializedObject(pipeline);
            var rendererListProp = so.FindProperty("m_RendererDataList");
            if (rendererListProp == null || rendererListProp.arraySize == 0) return;

            var rendererData = rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue;
            if (rendererData == null || AssetDatabase.Contains(rendererData)) return;

            rendererData.name = "SteadingUniversalRenderer";
            AssetDatabase.AddObjectToAsset(rendererData, pipeline);
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipeline);
        }

        private static void RepairQualitySettings(RenderPipelineAsset pipeline)
        {
            var qualitySettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qualitySettingsAssets == null || qualitySettingsAssets.Length == 0) return;

            var so = new SerializedObject(qualitySettingsAssets[0]);
            var qualitySettings = so.FindProperty("m_QualitySettings");
            if (qualitySettings == null) return;

            for (int i = 0; i < qualitySettings.arraySize; i++)
            {
                var qualityLevel = qualitySettings.GetArrayElementAtIndex(i);
                var customRenderPipeline = qualityLevel.FindPropertyRelative("customRenderPipeline");
                if (customRenderPipeline != null) customRenderPipeline.objectReferenceValue = pipeline;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(qualitySettingsAssets[0]);
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
