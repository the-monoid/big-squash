using Steading.Art;
using UnityEngine;

namespace Steading.Building
{
    public class BuildableVisualEnhancer : MonoBehaviour
    {
        private const string VisualRootName = "BuildableVisuals";

        private Material _woodA;
        private Material _woodB;
        private Material _woodDark;
        private Material _stoneA;
        private Material _stoneB;
        private Material _iron;

        private void Awake()
        {
            if (transform.Find(VisualRootName) != null) return;

            CreateMaterials();
            HideSourceRenderers();

            var root = new GameObject(VisualRootName).transform;
            root.SetParent(transform, false);

            var key = gameObject.name.ToLowerInvariant();
            if (key.Contains("floor")) BuildFloor(root);
            else if (key.Contains("pillar")) BuildPillar(root);
            else if (key.Contains("doorway")) BuildDoorway(root);
            else BuildWall(root);
        }

        private void HideSourceRenderers()
        {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform.GetComponentInParent<BuildableVisualEnhancer>() != null && renderer.transform.name == VisualRootName) continue;
                renderer.enabled = false;
            }
        }

        private void BuildWall(Transform root)
        {
            for (int i = 0; i < 6; i++)
            {
                var x = Mathf.Lerp(-0.42f, 0.42f, i / 5f);
                var mat = i % 2 == 0 ? _woodA : _woodB;
                var z = i % 2 == 0 ? -0.030f : 0.030f;
                Cube(root, "VerticalPlank_" + i, new Vector3(x, 0f, z), new Vector3(0.145f, 0.96f, 1.12f), mat);
                Cube(root, "PlankHighlight_" + i, new Vector3(x - 0.050f, 0.01f, -0.58f), new Vector3(0.018f, 0.90f, 0.035f), _woodDark);
            }

            Cube(root, "TopBeam", new Vector3(0f, 0.48f, 0f), new Vector3(1.08f, 0.105f, 1.20f), _woodDark);
            Cube(root, "BottomBeam", new Vector3(0f, -0.48f, 0f), new Vector3(1.08f, 0.105f, 1.20f), _woodDark);
            Cube(root, "CrossBraceA", new Vector3(-0.22f, 0f, -0.64f), new Vector3(0.105f, 1.08f, 0.080f), _woodDark, new Vector3(0f, 0f, -31f));
            Cube(root, "CrossBraceB", new Vector3(0.22f, 0f, -0.66f), new Vector3(0.105f, 1.08f, 0.080f), _woodDark, new Vector3(0f, 0f, 31f));

            for (int i = 0; i < 8; i++)
            {
                var x = i < 4 ? -0.47f : 0.47f;
                var y = Mathf.Lerp(-0.34f, 0.34f, (i % 4) / 3f);
                Cube(root, "IronRivet_" + i, new Vector3(x, y, -0.72f), new Vector3(0.045f, 0.045f, 0.040f), _iron);
            }
        }

        private void BuildFloor(Transform root)
        {
            for (int i = 0; i < 7; i++)
            {
                var x = Mathf.Lerp(-0.43f, 0.43f, i / 6f);
                var mat = i % 2 == 0 ? _woodA : _woodB;
                Cube(root, "FloorBoard_" + i, new Vector3(x, 0.54f, 0f), new Vector3(0.120f, 0.110f, 1.02f), mat);
                Cube(root, "FloorGroove_" + i, new Vector3(x + 0.060f, 0.605f, 0f), new Vector3(0.010f, 0.022f, 1.04f), _woodDark);
            }

            Cube(root, "FrontTrim", new Vector3(0f, 0.48f, -0.52f), new Vector3(1.05f, 0.16f, 0.070f), _woodDark);
            Cube(root, "BackTrim", new Vector3(0f, 0.48f, 0.52f), new Vector3(1.05f, 0.16f, 0.070f), _woodDark);
            Cube(root, "LeftTrim", new Vector3(-0.52f, 0.48f, 0f), new Vector3(0.070f, 0.16f, 1.05f), _woodDark);
            Cube(root, "RightTrim", new Vector3(0.52f, 0.48f, 0f), new Vector3(0.070f, 0.16f, 1.05f), _woodDark);
        }

        private void BuildPillar(Transform root)
        {
            for (int i = 0; i < 7; i++)
            {
                var y = Mathf.Lerp(-0.39f, 0.39f, i / 6f);
                var mat = i % 2 == 0 ? _stoneA : _stoneB;
                Cube(root, "StoneCourse_" + i, new Vector3(0f, y, 0f), new Vector3(1.10f, 0.115f, 1.10f), mat);
                Cube(root, "MortarLine_" + i, new Vector3(0f, y + 0.060f, -0.57f), new Vector3(1.00f, 0.012f, 0.030f), _stoneB);
            }

            Cube(root, "CapTop", new Vector3(0f, 0.52f, 0f), new Vector3(1.32f, 0.080f, 1.32f), _stoneB);
            Cube(root, "CapBottom", new Vector3(0f, -0.52f, 0f), new Vector3(1.32f, 0.080f, 1.32f), _stoneB);
            Cube(root, "MetalBandTop", new Vector3(0f, 0.43f, 0f), new Vector3(1.18f, 0.035f, 1.18f), _iron);
            Cube(root, "MetalBandBottom", new Vector3(0f, -0.43f, 0f), new Vector3(1.18f, 0.035f, 1.18f), _iron);
        }

        private void BuildDoorway(Transform root)
        {
            DoorPost(root, "Left", -0.8f);
            DoorPost(root, "Right", 0.8f);
            Cube(root, "HeaderBeam", new Vector3(0f, 1.25f, 0f), new Vector3(1.24f, 0.46f, 0.24f), _woodDark);
            Cube(root, "HeaderFace", new Vector3(0f, 1.25f, -0.14f), new Vector3(1.12f, 0.32f, 0.045f), _woodA);
            Cube(root, "Threshold", new Vector3(0f, -1.46f, 0f), new Vector3(1.58f, 0.095f, 0.30f), _woodDark);
            Cube(root, "RunePlate", new Vector3(0f, 1.25f, -0.18f), new Vector3(0.30f, 0.16f, 0.028f), _iron);
        }

        private void DoorPost(Transform root, string side, float x)
        {
            Cube(root, side + "PostCore", new Vector3(x, 0f, 0f), new Vector3(0.38f, 2.95f, 0.24f), _woodDark);
            for (int i = 0; i < 5; i++)
            {
                var y = Mathf.Lerp(-1.05f, 1.05f, i / 4f);
                Cube(root, side + "PostBand_" + i, new Vector3(x, y, -0.145f), new Vector3(0.44f, 0.065f, 0.045f), i % 2 == 0 ? _iron : _woodA);
            }
        }

        private void Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material mat)
        {
            Cube(parent, name, localPosition, localScale, mat, Vector3.zero);
        }

        private void Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material mat, Vector3 localEuler)
        {
            var go = new GameObject(name);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);

            var seed = ProceduralArt.StableSeed(gameObject.name + ":" + name);
            var wobble = Mathf.Min(0.025f, Mathf.Max(0.004f, localScale.magnitude * 0.012f));
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.CreateRoughBoxMesh(name + "Mesh", localScale, seed, wobble, 3);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void CreateMaterials()
        {
            _woodA = CreateMaterial("RuntimeBuildWoodA", new Color(0.46f, 0.29f, 0.15f), 0.42f, 0f);
            _woodB = CreateMaterial("RuntimeBuildWoodB", new Color(0.36f, 0.22f, 0.11f), 0.36f, 0f);
            _woodDark = CreateMaterial("RuntimeBuildWoodDark", new Color(0.19f, 0.105f, 0.050f), 0.30f, 0f);
            _stoneA = CreateMaterial("RuntimeBuildStoneA", new Color(0.43f, 0.44f, 0.42f), 0.48f, 0f);
            _stoneB = CreateMaterial("RuntimeBuildStoneB", new Color(0.31f, 0.32f, 0.31f), 0.40f, 0f);
            _iron = CreateMaterial("RuntimeBuildIron", new Color(0.25f, 0.25f, 0.24f), 0.38f, 0.22f);
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            return ProceduralArt.CreateLitMaterial(name, color, SurfaceForMaterial(name), smoothness, metallic);
        }

        private static ArtSurface SurfaceForMaterial(string name)
        {
            if (name.Contains("Wood")) return ArtSurface.Wood;
            if (name.Contains("Stone")) return ArtSurface.Stone;
            if (name.Contains("Iron")) return ArtSurface.DarkMetal;
            return ArtSurface.Plain;
        }
    }
}
