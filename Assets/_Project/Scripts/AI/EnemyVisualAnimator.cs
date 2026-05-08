using System.Collections;
using System.Collections.Generic;
using Steading.Art;
using UnityEngine;

namespace Steading.AI
{
    public class EnemyVisualAnimator : MonoBehaviour, IEnemyVisuals
    {
        // Bumped from (14, 28, 24) so Draugr silhouettes are smooth instead of
        // visibly faceted at melee range.
        private const int BodyLatitude = 28;
        private const int BodyLongitude = 56;
        private const int LimbSegments = 48;

        private Transform _rig;
        private Transform _hips;
        private Transform _torso;
        private Transform _head;
        private Transform _leftShoulder;
        private Transform _rightShoulder;
        private Transform _leftElbow;
        private Transform _rightElbow;
        private Transform _leftHip;
        private Transform _rightHip;
        private Transform _leftKnee;
        private Transform _rightKnee;
        private Transform _leftFoot;
        private Transform _rightFoot;

        private Vector3 _lastPosition;
        private float _cycle;
        private float _speed;
        private float _attackWeight;
        private float _attackSide;
        private float _attackLift;
        private float _staggerWeight;
        private Coroutine _attackRoutine;
        private Coroutine _staggerRoutine;
        private Material _skin;
        private Material _rag;
        private Material _bone;
        private Material _eye;
        private Material _leather;
        private Material _metal;

        private void Awake()
        {
            EnsureRig();
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            Animate();
        }

        public void EnsureRig()
        {
            HideLegacyVisual();
            BuildRig();
        }

        public void PlayAttack(int variant)
        {
            EnsureRig();
            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(AttackRoutine(variant));
        }

        public void PlayStagger(float seconds)
        {
            EnsureRig();
            if (_staggerRoutine != null) StopCoroutine(_staggerRoutine);
            _staggerRoutine = StartCoroutine(StaggerRoutine(seconds));
        }

        private void HideLegacyVisual()
        {
            var legacy = transform.Find("Visual");
            if (legacy == null) return;

            foreach (var renderer in legacy.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }
        }

        private void BuildRig()
        {
            if (_rig != null) return;

            var oldRig = transform.Find("EnemyRig");
            if (oldRig != null)
            {
                if (Application.isPlaying) Destroy(oldRig.gameObject);
                else DestroyImmediate(oldRig.gameObject);
            }

            _skin = CreateMaterial("DraugrSkin", new Color(0.23f, 0.33f, 0.30f), 0.42f, 0f);
            _rag = CreateMaterial("DraugrRags", new Color(0.18f, 0.17f, 0.15f), 0.50f, 0f);
            _bone = CreateMaterial("DraugrBone", new Color(0.62f, 0.58f, 0.48f), 0.45f, 0f);
            _eye = CreateMaterial("DraugrEyeGlow", new Color(0.45f, 0.95f, 0.72f), 0.12f, 0f);
            _leather = CreateMaterial("DraugrLeather", new Color(0.20f, 0.11f, 0.06f), 0.30f, 0f);
            _metal = CreateMaterial("DraugrRust", new Color(0.34f, 0.28f, 0.22f), 0.46f, 0.18f);

            _rig = new GameObject("EnemyRig").transform;
            _rig.SetParent(transform, false);
            _rig.localPosition = Vector3.zero;

            _hips = Joint("Hips", _rig, new Vector3(0f, 0.86f, 0f));
            Ellipsoid("Pelvis", _hips, Vector3.zero, new Vector3(0.30f, 0.15f, 0.22f), _rag);
            _torso = Joint("Torso", _hips, new Vector3(0f, 0.30f, 0f));
            Ellipsoid("Ribcage", _torso, new Vector3(0f, 0.12f, 0f), new Vector3(0.35f, 0.34f, 0.23f), _skin);
            Ellipsoid("RaggedTunic", _torso, new Vector3(0f, 0.06f, 0.015f), new Vector3(0.38f, 0.28f, 0.25f), _rag);
            Tube("SpineBone", _torso, new Vector3(0f, 0.13f, -0.235f), 0.48f, 0.030f, 0.020f, _bone, new Vector3(0f, 0f, 0f));
            CreateRibDetails();

            _head = Joint("Head", _torso, new Vector3(0f, 0.48f, 0.03f));
            Ellipsoid("HeadMesh", _head, Vector3.zero, new Vector3(0.19f, 0.25f, 0.18f), _skin);
            Ellipsoid("Jaw", _head, new Vector3(0f, -0.12f, 0.04f), new Vector3(0.15f, 0.09f, 0.12f), _skin);
            Ellipsoid("LeftEye", _head, new Vector3(-0.065f, 0.045f, 0.165f), new Vector3(0.023f, 0.014f, 0.010f), _eye);
            Ellipsoid("RightEye", _head, new Vector3(0.065f, 0.045f, 0.165f), new Vector3(0.023f, 0.014f, 0.010f), _eye);
            Tube("RottenBeard", _head, new Vector3(0f, -0.15f, 0.14f), 0.20f, 0.055f, 0.030f, _rag, new Vector3(0f, 0f, 0f));
            Ellipsoid("LeftCheekBone", _head, new Vector3(-0.082f, -0.030f, 0.145f), new Vector3(0.045f, 0.025f, 0.020f), _bone);
            Ellipsoid("RightCheekBone", _head, new Vector3(0.082f, -0.030f, 0.145f), new Vector3(0.045f, 0.025f, 0.020f), _bone);
            Tube("RottenTopKnot", _head, new Vector3(0f, 0.210f, -0.035f), 0.22f, 0.038f, 0.018f, _rag, new Vector3(-18f, 0f, 0f));

            BuildArm(true);
            BuildArm(false);
            BuildLeg(true);
            BuildLeg(false);
        }

        private void BuildArm(bool left)
        {
            var sign = left ? -1f : 1f;
            var shoulder = Joint(left ? "LeftShoulder" : "RightShoulder", _torso, new Vector3(sign * 0.35f, 0.31f, 0f));
            var elbow = Limb(left ? "LeftUpperArm" : "RightUpperArm", shoulder, 0.38f, 0.080f, 0.060f, _skin);
            var wrist = Limb(left ? "LeftForearm" : "RightForearm", elbow, 0.34f, 0.064f, 0.046f, _skin);
            Ellipsoid((left ? "Left" : "Right") + "Claw", wrist, new Vector3(0f, -0.06f, 0.02f), new Vector3(0.070f, 0.055f, 0.045f), _bone);
            Tube((left ? "Left" : "Right") + "WristWrap", elbow, new Vector3(0f, -0.23f, 0f), 0.11f, 0.070f, 0.048f, _leather, Vector3.zero);
            if (!left)
            {
                Tube("RustyKnifeGrip", wrist, new Vector3(0f, -0.110f, 0.025f), 0.22f, 0.018f, 0.014f, _leather, Vector3.zero);
                Tube("RustyKnifeBlade", wrist, new Vector3(0f, -0.260f, 0.045f), 0.38f, 0.034f, 0.011f, _metal, new Vector3(7f, 0f, 0f));
            }

            if (left)
            {
                _leftShoulder = shoulder;
                _leftElbow = elbow;
            }
            else
            {
                _rightShoulder = shoulder;
                _rightElbow = elbow;
            }
        }

        private void BuildLeg(bool left)
        {
            var sign = left ? -1f : 1f;
            var hip = Joint(left ? "LeftHip" : "RightHip", _hips, new Vector3(sign * 0.15f, -0.08f, 0f));
            var knee = Limb(left ? "LeftThigh" : "RightThigh", hip, 0.42f, 0.090f, 0.065f, _rag);
            var ankle = Limb(left ? "LeftShin" : "RightShin", knee, 0.36f, 0.070f, 0.050f, _skin);
            var foot = Joint((left ? "Left" : "Right") + "Foot", ankle, new Vector3(0f, -0.055f, 0.10f));
            Ellipsoid((left ? "Left" : "Right") + "FootMesh", foot, Vector3.zero, new Vector3(0.080f, 0.045f, 0.145f), _skin);

            if (left)
            {
                _leftHip = hip;
                _leftKnee = knee;
                _leftFoot = foot;
            }
            else
            {
                _rightHip = hip;
                _rightKnee = knee;
                _rightFoot = foot;
            }
        }

        private Transform Limb(string name, Transform parent, float length, float topRadius, float bottomRadius, Material material)
        {
            var end = Joint(name + "End", parent, new Vector3(0f, -length, 0f));
            Tube(name, parent, Vector3.zero, length, topRadius, bottomRadius, material, Vector3.zero);
            return end;
        }

        private void CreateRibDetails()
        {
            for (int i = 0; i < 5; i++)
            {
                var y = 0.255f - i * 0.068f;
                var width = 0.34f - i * 0.020f;
                Tube("LeftRib_" + i, _torso, new Vector3(-width * 0.42f, y, 0.215f), width, 0.014f, 0.009f, _bone, new Vector3(83f, 0f, 82f));
                Tube("RightRib_" + i, _torso, new Vector3(width * 0.42f, y, 0.215f), width, 0.014f, 0.009f, _bone, new Vector3(83f, 0f, -82f));
            }

            Tube("RaggedSash", _torso, new Vector3(-0.035f, 0.120f, 0.260f), 0.70f, 0.030f, 0.018f, _leather, new Vector3(84f, 0f, -31f));
            Ellipsoid("RustyChestBoss", _torso, new Vector3(0.045f, 0.140f, 0.285f), new Vector3(0.065f, 0.050f, 0.018f), _metal);
        }

        private void Animate()
        {
            var delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f;
            _speed = Mathf.Lerp(_speed, Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f, 1f - Mathf.Exp(-10f * Time.deltaTime));

            var moving = _speed > 0.06f;
            if (moving) _cycle += Time.deltaTime * Mathf.Lerp(4.8f, 7.2f, Mathf.Clamp01(_speed / 3.2f));
            var s = Mathf.Sin(_cycle);
            var o = Mathf.Sin(_cycle + Mathf.PI);
            var lift = Mathf.Abs(Mathf.Cos(_cycle)) * 0.035f;

            SetPos(_hips, new Vector3(0f, 0.86f + (moving ? lift : 0f), 0f));
            SetRot(_hips, Quaternion.Euler(_staggerWeight * -12f, 0f, moving ? -s * 3f : 0f));
            SetRot(_torso, Quaternion.Euler(_staggerWeight * -18f + _attackLift * 8f + (moving ? 5f : 0f), _attackSide * 12f, moving ? o * 5f : 0f));
            SetRot(_head, Quaternion.Euler(_staggerWeight * 14f, 0f, moving ? s * 2f : 0f));

            SetRot(_leftShoulder, Quaternion.Euler(_attackWeight > 0f && _attackSide > 0f ? Mathf.Lerp(8f, -72f, _attackWeight) : (moving ? o * 34f : 8f), _attackSide > 0f ? -18f * _attackWeight : 0f, -12f));
            SetRot(_rightShoulder, Quaternion.Euler(_attackWeight > 0f && _attackSide <= 0f ? Mathf.Lerp(12f, -78f, _attackWeight) : (moving ? s * 34f : 8f), _attackSide <= 0f ? 18f * _attackWeight : 0f, 12f));
            SetRot(_leftElbow, Quaternion.Euler(_attackWeight > 0f && _attackSide > 0f ? Mathf.Lerp(18f, 72f, _attackWeight) : 18f, 0f, 0f));
            SetRot(_rightElbow, Quaternion.Euler(_attackWeight > 0f && _attackSide <= 0f ? Mathf.Lerp(18f, 74f, _attackWeight) : 18f, 0f, 0f));
            SetRot(_leftHip, Quaternion.Euler(moving ? s * 32f : 0f, 0f, 0f));
            SetRot(_rightHip, Quaternion.Euler(moving ? o * 32f : 0f, 0f, 0f));
            SetRot(_leftKnee, Quaternion.Euler(moving ? Mathf.Max(0f, -s) * 28f : 4f, 0f, 0f));
            SetRot(_rightKnee, Quaternion.Euler(moving ? Mathf.Max(0f, -o) * 28f : 4f, 0f, 0f));
            SetPos(_leftFoot, new Vector3(0f, -0.055f + Mathf.Max(0f, s) * 0.035f, 0.10f));
            SetPos(_rightFoot, new Vector3(0f, -0.055f + Mathf.Max(0f, o) * 0.035f, 0.10f));
        }

        private IEnumerator AttackRoutine(int variant)
        {
            _attackSide = variant % 2 == 0 ? -1f : 1f;
            _attackLift = variant >= 2 ? 1f : 0f;
            yield return BlendAttack(0.45f, 0.16f);
            yield return BlendAttack(1f, 0.08f);
            yield return BlendAttack(0f, 0.20f);
            _attackLift = 0f;
            _attackRoutine = null;
        }

        private IEnumerator StaggerRoutine(float seconds)
        {
            _staggerWeight = 1f;
            yield return new WaitForSeconds(Mathf.Max(0.05f, seconds));
            var elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.deltaTime;
                _staggerWeight = Mathf.Lerp(1f, 0f, elapsed / 0.25f);
                yield return null;
            }
            _staggerWeight = 0f;
            _staggerRoutine = null;
        }

        private IEnumerator BlendAttack(float target, float duration)
        {
            var start = _attackWeight;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _attackWeight = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            _attackWeight = target;
        }

        private Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            return joint;
        }

        private Transform Ellipsoid(string name, Transform parent, Vector3 localPosition, Vector3 radius, Material material)
        {
            return MeshPart(name, parent, localPosition, Quaternion.identity, BuildEllipsoid(radius, BodyLatitude, BodyLongitude), material);
        }

        private Transform Tube(string name, Transform parent, Vector3 localPosition, float length, float topRadius, float bottomRadius, Material material, Vector3 euler)
        {
            return MeshPart(name, parent, localPosition, Quaternion.Euler(euler), BuildTube(length, topRadius, bottomRadius, LimbSegments), material);
        }

        private Transform MeshPart(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            go.gameObject.isStatic = false;
            return go.transform;
        }

        private static Mesh BuildEllipsoid(Vector3 radius, int latitude, int longitude)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int lat = 0; lat <= latitude; lat++)
            {
                var phi = lat / (float)latitude * Mathf.PI;
                for (int lon = 0; lon <= longitude; lon++)
                {
                    var theta = lon / (float)longitude * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(theta) * Mathf.Sin(phi) * radius.x, Mathf.Cos(phi) * radius.y, Mathf.Sin(theta) * Mathf.Sin(phi) * radius.z));
                }
            }
            var row = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    var a = lat * row + lon;
                    var b = a + row;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            return Finish("EnemyEllipsoid", vertices, triangles);
        }

        private static Mesh BuildTube(float length, float topRadius, float bottomRadius, int segments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int ring = 0; ring < 2; ring++)
            {
                var t = ring;
                var y = -length * t;
                var r = Mathf.Lerp(topRadius, bottomRadius, t);
                for (int i = 0; i < segments; i++)
                {
                    var a = i / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
            }
            for (int i = 0; i < segments; i++)
            {
                var ni = (i + 1) % segments;
                triangles.Add(i); triangles.Add(segments + i); triangles.Add(ni);
                triangles.Add(ni); triangles.Add(segments + i); triangles.Add(segments + ni);
            }

            var topCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            var bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -length, 0f));
            for (int i = 0; i < segments; i++)
            {
                var ni = (i + 1) % segments;
                triangles.Add(topCenter); triangles.Add(ni); triangles.Add(i);
                triangles.Add(bottomCenter); triangles.Add(segments + i); triangles.Add(segments + ni);
            }
            return Finish("EnemyTube", vertices, triangles);
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            return ProceduralArt.CreateLitMaterial(name, color, SurfaceForMaterial(name), smoothness, metallic);
        }

        private static ArtSurface SurfaceForMaterial(string name)
        {
            if (name.Contains("Skin")) return ArtSurface.Skin;
            if (name.Contains("Rag")) return ArtSurface.Cloth;
            if (name.Contains("Bone")) return ArtSurface.Bone;
            if (name.Contains("Eye")) return ArtSurface.EyeGlow;
            if (name.Contains("Leather")) return ArtSurface.Leather;
            if (name.Contains("Rust") || name.Contains("Iron")) return ArtSurface.DarkMetal;
            return ArtSurface.Plain;
        }

        private static void SetRot(Transform target, Quaternion rotation)
        {
            if (target != null) target.localRotation = rotation;
        }

        private static void SetPos(Transform target, Vector3 position)
        {
            if (target != null) target.localPosition = position;
        }
    }
}
