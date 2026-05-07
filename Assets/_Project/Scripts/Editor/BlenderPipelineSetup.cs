using System.IO;
using UnityEditor;
using UnityEngine;

namespace Steading.EditorTools
{
    public static class BlenderPipelineSetup
    {
        private const string ModelsRoot = "Assets/_Project/Art/Models";
        private const string MaterialsRoot = "Assets/_Project/Art/Materials";
        private const string TexturesRoot = "Assets/_Project/Art/Textures";
        private const string PrefabRoot = "Assets/_Project/Prefabs/Art";
        private const string SourceExportsRoot = "SourceArt/Blender/exports";

        [MenuItem("Steading/Art Pipeline/Prepare Folders")]
        public static void PrepareFolders()
        {
            EnsureFolder(ModelsRoot);
            EnsureFolder(ModelsRoot + "/Characters/Player");
            EnsureFolder(ModelsRoot + "/Characters/Enemies");
            EnsureFolder(ModelsRoot + "/Weapons");
            EnsureFolder(ModelsRoot + "/Buildables");
            EnsureFolder(ModelsRoot + "/World");
            EnsureFolder(MaterialsRoot);
            EnsureFolder(TexturesRoot);
            EnsureFolder(PrefabRoot);
            AssetDatabase.Refresh();
            Debug.Log("[Steading] Blender art pipeline folders are ready.");
        }

        [MenuItem("Steading/Art Pipeline/Apply Model Import Settings")]
        public static void ApplyModelImportSettings()
        {
            PrepareFolders();
            var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot });
            foreach (var guid in modelGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                ConfigureImporter(importer, path);
                importer.SaveAndReimport();
            }

            Debug.Log($"[Steading] Applied import settings to {modelGuids.Length} model assets.");
        }

        [MenuItem("Steading/Art Pipeline/Import Generated SourceArt Models")]
        public static void ImportGeneratedSourceArtModels()
        {
            PrepareFolders();

            var projectRoot = Directory.GetCurrentDirectory();
            var sourceRoot = Path.Combine(projectRoot, SourceExportsRoot);
            var destinationRoot = Path.Combine(projectRoot, ModelsRoot);
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogWarning("[Steading] No generated SourceArt exports found. Run SourceArt/Blender/generate_steading_art_pack.py first.");
                return;
            }

            var copied = 0;
            foreach (var file in Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".obj" && extension != ".mtl" && extension != ".fbx" && extension != ".glb") continue;

                var relative = file.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destination = Path.Combine(destinationRoot, relative);
                var destinationDirectory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);
                File.Copy(file, destination, overwrite: true);
                copied++;
            }

            AssetDatabase.Refresh();
            ApplyModelImportSettings();
            Debug.Log($"[Steading] Imported {copied} generated SourceArt model files.");
        }

        [MenuItem("Steading/Art Pipeline/Create Prefabs From Selected Models")]
        public static void CreatePrefabsFromSelectedModels()
        {
            PrepareFolders();
            var created = 0;
            foreach (var selected in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrEmpty(path)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                var prefabPath = PrefabRoot + "/" + Path.GetFileNameWithoutExtension(path) + ".prefab";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (instance == null) continue;

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Steading] Created {created} art prefabs.");
        }

        private static void ConfigureImporter(ModelImporter importer, string path)
        {
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            var normalized = path.Replace('\\', '/').ToLowerInvariant();
            if (normalized.Contains("/characters/player/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
            }
            else if (normalized.Contains("/characters/enemies/"))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }

        private static void EnsureFolder(string path)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        }
    }
}
