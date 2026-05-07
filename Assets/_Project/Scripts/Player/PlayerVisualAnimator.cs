using System.Collections;
using System.Collections.Generic;
using Steading.Art;
using UnityEngine;

namespace Steading.Player
{
    public class PlayerVisualAnimator : MonoBehaviour
    {
        // Higher segment counts → smoother silhouettes. Doubled from prior values
        // to reduce the "geometric blob" read at the cost of ~3× vertex count per
        // character. Still well within budget for 8-player co-op on URP.
        private const int LimbSegments = 64;
        private const int BodySegments = 96;

        [Header("Movement")]
        [SerializeField] private float walkThreshold = 0.08f;
        [SerializeField] private float runThreshold = 5.4f;
        [SerializeField] private float speedSmoothing = 13f;
        [SerializeField] private float turnSmoothing = 10f;
        [SerializeField] private float walkCycleRate = 6.4f;
        [SerializeField] private float runCycleRate = 10.8f;

        [Header("Idle")]
        [SerializeField] private float idleBreathRate = 0.42f;     // breaths per second
        [SerializeField] private float idleBreathDepth = 0.013f;
        [SerializeField] private float idleSwayRate = 0.28f;
        [SerializeField] private float idleSwayAmount = 0.008f;
        [SerializeField] private float idleHeadBobAmount = 0.6f;   // degrees of subtle head drift

        [Header("Look")]
        [SerializeField] private float headFollowCamera = 0.55f;
        [SerializeField] private float headPitchMax = 38f;
        [SerializeField] private float headPitchMin = -28f;

        [Header("Jump / Air")]
        [SerializeField] private float landRecoverySeconds = 0.22f;
        [SerializeField] private float jumpAnticipationSeconds = 0.10f;

        private Transform _rig;
        private Transform _hips;
        private Transform _torso;
        private Transform _chest;
        private Transform _head;
        private Transform _leftShoulder;
        private Transform _rightShoulder;
        private Transform _leftElbow;
        private Transform _rightElbow;
        private Transform _leftWrist;
        private Transform _rightWrist;
        private Transform _leftHip;
        private Transform _rightHip;
        private Transform _leftKnee;
        private Transform _rightKnee;
        private Transform _leftAnkle;
        private Transform _rightAnkle;
        private Transform _leftFootMesh;
        private Transform _rightFootMesh;
        private Transform _leftHandSocket;
        private Transform _rightHandSocket;

        private Vector3 _lastPosition;
        private Vector3 _lastForward;
        private float _cycle;
        private float _smoothedSpeed;
        private float _turnAmount;
        private float _verticalVelocity;       // sign + magnitude of vertical motion
        private float _smoothedVertical;
        private float _airTime;                // seconds since last grounded
        private float _landRecoveryT;          // 1 -> 0 over landRecoverySeconds after landing
        private bool  _wasAirborneLastFrame;
        private float _smoothedHeadPitch;
        private float _attackWeight;
        private Vector3 _attackTorsoEuler;
        private Vector3 _attackRightShoulderEuler;
        private Vector3 _attackRightElbowEuler;
        private Vector3 _attackRightWristEuler;
        private Vector3 _attackLeftShoulderEuler;
        private Coroutine _attackRoutine;

        private Material _skin;
        private Material _skinWarm;
        private Material _tunic;
        private Material _pants;
        private Material _boots;
        private Material _leather;
        private Material _wood;
        private Material _fur;
        private Material _hair;
        private Material _metal;
        private Material _darkMetal;
        private Material _cloth;
        private Material _eye;
        private CharacterCustomization _appearance = CharacterCustomization.Default;

        public float CurrentSpeed => _smoothedSpeed;

        public Transform RightHandSocket
        {
            get
            {
                EnsureRig();
                return _rightHandSocket;
            }
        }

        public Transform LeftHandSocket
        {
            get
            {
                EnsureRig();
                return _leftHandSocket;
            }
        }

        private void Awake()
        {
            EnsureRig();
            _lastPosition = transform.position;
            _lastForward = transform.forward;
        }

        private void OnDisable()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
            _attackWeight = 0f;
        }

        private void LateUpdate()
        {
            AnimateFromMovement();
        }

        public void EnsureRig()
        {
            HideLegacyVisual();
            BuildRig();
        }

        public void ApplyCustomization(CharacterCustomization customization)
        {
            _appearance = customization.Sanitized();
            if (_rig == null)
            {
                EnsureRig();
                return;
            }

            ApplyAppearanceToRig();
        }

        public void PlaySwordAttackPose(bool heavy, int comboStep)
        {
            EnsureRig();
            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(AnimateSwordAttackPose(heavy, comboStep));
        }

        public void PlayShieldBashPose()
        {
            EnsureRig();
            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(AnimateShieldBashPose());
        }

        public void PlaySkillAttackPose(bool axe)
        {
            EnsureRig();
            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(AnimateSkillAttackPose(axe));
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

            var oldRig = transform.Find("CharacterRig");
            if (oldRig != null)
            {
                oldRig.name = "OldCharacterRig";
                DestroyUnityObject(oldRig.gameObject);
            }

            CreateMaterials();

            _rig = new GameObject("CharacterRig").transform;
            _rig.SetParent(transform, false);
            _rig.localPosition = Vector3.zero;
            _rig.localRotation = Quaternion.identity;

            _hips = CreateJoint("Hips", _rig, new Vector3(0f, 0.92f, 0f));
            CreateEllipsoid("Pelvis", _hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.15f, 0.215f), _pants);
            CreateTorus("Belt", _hips, new Vector3(0f, 0.10f, 0.01f), 0.315f, 0.205f, 0.026f, _leather);
            CreateEllipsoid("BeltBuckle", _hips, new Vector3(0f, 0.11f, 0.22f), new Vector3(0.080f, 0.055f, 0.020f), _metal);
            CreateClothPanel("LeftTunicPanel", _hips, new Vector3(-0.12f, 0.03f, 0.205f), 0.27f, 0.20f, 0.42f, 0.025f, _tunic);
            CreateClothPanel("RightTunicPanel", _hips, new Vector3(0.12f, 0.03f, 0.205f), 0.27f, 0.20f, 0.42f, -0.025f, _tunic);

            _torso = CreateJoint("Torso", _hips, new Vector3(0f, 0.28f, 0f));
            CreateEllipsoid("Abdomen", _torso, new Vector3(0f, 0.07f, 0.01f), new Vector3(0.305f, 0.275f, 0.220f), _tunic);
            CreateStrap("CrossStrapA", _torso, new Vector3(-0.11f, 0.16f, 0.235f), 0.64f, 0.028f, _leather, new Vector3(0f, 0f, -27f));
            CreateStrap("CrossStrapB", _torso, new Vector3(0.11f, 0.16f, 0.238f), 0.64f, 0.028f, _leather, new Vector3(0f, 0f, 27f));
            CreateEllipsoid("StrapKnot", _torso, new Vector3(0f, 0.13f, 0.27f), new Vector3(0.055f, 0.045f, 0.030f), _darkMetal);

            _chest = CreateJoint("Chest", _torso, new Vector3(0f, 0.31f, 0f));
            CreateEllipsoid("ChestMail", _chest, new Vector3(0f, 0.02f, 0f), new Vector3(0.365f, 0.285f, 0.245f), _cloth);
            CreateChainmailScales(_chest);
            CreateFurCollar(_chest);
            CreateShoulderArmor(_chest);
            CreateClothPanel("CloakBack", _chest, new Vector3(0f, 0.19f, -0.245f), 0.70f, 0.54f, 0.86f, 0f, _cloth, new Vector3(8f, 180f, 0f));

            _head = CreateJoint("Head", _chest, new Vector3(0f, 0.32f, 0.04f));
            CreateHead();

            BuildArm(leftSide: true);
            BuildArm(leftSide: false);
            BuildLeg(leftSide: true);
            BuildLeg(leftSide: false);
            ApplyAppearanceToRig();
        }

        private void ApplyAppearanceToRig()
        {
            var appearance = _appearance.Sanitized();
            _appearance = appearance;

            if (_rig != null)
            {
                _rig.localScale = new Vector3(appearance.buildScale, appearance.heightScale, appearance.buildScale);
            }

            SetMaterialColor(_skin, appearance.skinColor);
            SetMaterialColor(_skinWarm, Color.Lerp(appearance.skinColor, Color.white, 0.11f));
            SetMaterialColor(_hair, appearance.hairColor);
            SetMaterialColor(_tunic, appearance.tunicColor);
            SetMaterialColor(_pants, appearance.pantsColor);
            SetMaterialColor(_cloth, appearance.cloakColor);

            SetChildrenActiveByName(_rig, "Beard", appearance.beardEnabled);
            SetChildrenActiveByName(_rig, "Moustache", appearance.beardEnabled);
            SetChildrenActiveByName(_rig, "Helmet", appearance.helmetEnabled);
        }

        private void CreateHead()
        {
            CreateEllipsoid("HeadMesh", _head, new Vector3(0f, 0f, 0f), new Vector3(0.195f, 0.270f, 0.182f), _skin);
            CreateEllipsoid("Jaw", _head, new Vector3(0f, -0.116f, 0.038f), new Vector3(0.150f, 0.118f, 0.142f), _skinWarm);
            CreateEllipsoid("Nose", _head, new Vector3(0f, 0.015f, 0.185f), new Vector3(0.036f, 0.075f, 0.055f), _skinWarm);
            CreateEllipsoid("LeftCheek", _head, new Vector3(-0.082f, -0.030f, 0.150f), new Vector3(0.052f, 0.046f, 0.035f), _skinWarm);
            CreateEllipsoid("RightCheek", _head, new Vector3(0.082f, -0.030f, 0.150f), new Vector3(0.052f, 0.046f, 0.035f), _skinWarm);
            CreateEllipsoid("LeftEar", _head, new Vector3(-0.192f, 0.005f, 0.020f), new Vector3(0.025f, 0.064f, 0.020f), _skinWarm);
            CreateEllipsoid("RightEar", _head, new Vector3(0.192f, 0.005f, 0.020f), new Vector3(0.025f, 0.064f, 0.020f), _skinWarm);
            CreateEllipsoid("LeftEye", _head, new Vector3(-0.072f, 0.062f, 0.176f), new Vector3(0.021f, 0.013f, 0.009f), _eye);
            CreateEllipsoid("RightEye", _head, new Vector3(0.072f, 0.062f, 0.176f), new Vector3(0.021f, 0.013f, 0.009f), _eye);
            CreateStrap("UpperLip", _head, new Vector3(0f, -0.055f, 0.196f), 0.112f, 0.009f, _skinWarm, new Vector3(84f, 0f, 90f));
            CreateStrap("LowerLip", _head, new Vector3(0f, -0.078f, 0.190f), 0.090f, 0.008f, _skinWarm, new Vector3(84f, 0f, 90f));
            CreateStrap("LeftEyebrow", _head, new Vector3(-0.072f, 0.096f, 0.172f), 0.102f, 0.010f, _hair, new Vector3(83f, 0f, 86f));
            CreateStrap("RightEyebrow", _head, new Vector3(0.072f, 0.096f, 0.172f), 0.102f, 0.010f, _hair, new Vector3(83f, 0f, -86f));

            for (int i = 0; i < 9; i++)
            {
                var x = Mathf.Lerp(-0.14f, 0.14f, i / 8f);
                var len = 0.20f + Mathf.Abs(x) * 0.55f;
                var angle = x * -55f;
                CreateTaperedTube("BeardStrand" + i, _head, new Vector3(x, -0.135f, 0.165f), len, 0.026f, 0.015f, 0.018f, 0.010f, _hair, new Vector3(0f, 0f, angle), 0.12f);
            }

            CreateStrap("MoustacheLeft", _head, new Vector3(-0.073f, -0.055f, 0.185f), 0.17f, 0.018f, _hair, new Vector3(84f, 0f, 74f));
            CreateStrap("MoustacheRight", _head, new Vector3(0.073f, -0.055f, 0.185f), 0.17f, 0.018f, _hair, new Vector3(84f, 0f, -74f));
            CreateTaperedTube("LeftBeardBraid", _head, new Vector3(-0.095f, -0.270f, 0.150f), 0.19f, 0.030f, 0.019f, 0.024f, 0.014f, _hair, new Vector3(0f, 0f, -8f), 0.20f);
            CreateTaperedTube("RightBeardBraid", _head, new Vector3(0.095f, -0.270f, 0.150f), 0.19f, 0.030f, 0.019f, 0.024f, 0.014f, _hair, new Vector3(0f, 0f, 8f), 0.20f);

            CreateEllipsoid("HelmetDome", _head, new Vector3(0f, 0.165f, -0.005f), new Vector3(0.235f, 0.145f, 0.220f), _metal);
            CreateTorus("HelmetBrowBand", _head, new Vector3(0f, 0.075f, 0.005f), 0.215f, 0.198f, 0.015f, _darkMetal);
            CreateStrap("HelmetNoseGuard", _head, new Vector3(0f, -0.010f, 0.197f), 0.24f, 0.016f, _darkMetal, Vector3.zero);
            CreateEllipsoid("HelmetLeftCheekGuard", _head, new Vector3(-0.188f, -0.005f, 0.025f), new Vector3(0.026f, 0.112f, 0.075f), _darkMetal);
            CreateEllipsoid("HelmetRightCheekGuard", _head, new Vector3(0.188f, -0.005f, 0.025f), new Vector3(0.026f, 0.112f, 0.075f), _darkMetal);
        }

        private void CreateChainmailScales(Transform parent)
        {
            for (int row = 0; row < 4; row++)
            {
                var y = 0.185f - row * 0.075f;
                var width = 0.48f - row * 0.045f;
                var count = 7 - (row / 2);
                for (int i = 0; i < count; i++)
                {
                    var x = Mathf.Lerp(-width * 0.5f, width * 0.5f, count == 1 ? 0.5f : i / (float)(count - 1));
                    CreateEllipsoid("MailScale" + row + "_" + i, parent, new Vector3(x, y, 0.255f), new Vector3(0.027f, 0.038f, 0.009f), _darkMetal);
                }
            }
        }

        private void CreateFurCollar(Transform parent)
        {
            for (int i = 0; i < 13; i++)
            {
                var t = i / 12f;
                var x = Mathf.Lerp(-0.41f, 0.41f, t);
                var z = -0.010f + Mathf.Abs(x) * 0.18f;
                var y = 0.225f - Mathf.Abs(x) * 0.09f;
                var scale = new Vector3(0.065f + Mathf.Abs(x) * 0.030f, 0.045f, 0.120f);
                CreateEllipsoid("FurCollarTuft" + i, parent, new Vector3(x, y, z), scale, _fur);
            }

            for (int i = 0; i < 5; i++)
            {
                var x = -0.38f - i * 0.035f;
                CreateTaperedTube("LeftShoulderFur" + i, parent, new Vector3(x, 0.145f - i * 0.015f, 0.01f), 0.21f, 0.038f, 0.027f, 0.060f, 0.038f, _fur, new Vector3(0f, 0f, -78f), 0.15f);
                x = 0.38f + i * 0.035f;
                CreateTaperedTube("RightShoulderFur" + i, parent, new Vector3(x, 0.145f - i * 0.015f, 0.01f), 0.21f, 0.038f, 0.027f, 0.060f, 0.038f, _fur, new Vector3(0f, 0f, 78f), 0.15f);
            }
        }

        private void CreateShoulderArmor(Transform parent)
        {
            CreateEllipsoid("LeftLeatherShoulderCap", parent, new Vector3(-0.365f, 0.115f, 0.020f), new Vector3(0.135f, 0.060f, 0.135f), _leather);
            CreateEllipsoid("RightLeatherShoulderCap", parent, new Vector3(0.365f, 0.115f, 0.020f), new Vector3(0.135f, 0.060f, 0.135f), _leather);
            CreateTorus("LeftShoulderRim", parent, new Vector3(-0.365f, 0.112f, 0.022f), 0.124f, 0.116f, 0.010f, _darkMetal, new Vector3(0f, 0f, 7f));
            CreateTorus("RightShoulderRim", parent, new Vector3(0.365f, 0.112f, 0.022f), 0.124f, 0.116f, 0.010f, _darkMetal, new Vector3(0f, 0f, -7f));
        }

        private void BuildArm(bool leftSide)
        {
            var sign = leftSide ? -1f : 1f;
            var shoulder = CreateJoint(leftSide ? "LeftShoulder" : "RightShoulder", _chest, new Vector3(sign * 0.385f, 0.13f, 0f));
            var elbow = CreateLimb(leftSide ? "LeftUpperArm" : "RightUpperArm", shoulder, 0.410f, 0.082f, 0.061f, 0.066f, 0.050f, _tunic);
            var wrist = CreateLimb(leftSide ? "LeftForearm" : "RightForearm", elbow, 0.380f, 0.068f, 0.050f, 0.046f, 0.037f, _skin);
            CreateTorus((leftSide ? "Left" : "Right") + "BracerTop", elbow, new Vector3(0f, -0.165f, 0f), 0.077f, 0.057f, 0.012f, _leather);
            CreateTaperedTube((leftSide ? "Left" : "Right") + "LeatherBracer", elbow, new Vector3(0f, -0.255f, 0f), 0.165f, 0.079f, 0.059f, 0.060f, 0.047f, _leather, Vector3.zero, 0.04f);
            CreateHand(leftSide, wrist);

            if (leftSide)
            {
                CreateRoundShield(wrist);
                _leftShoulder = shoulder;
                _leftElbow = elbow;
                _leftWrist = wrist;
            }
            else
            {
                _rightShoulder = shoulder;
                _rightElbow = elbow;
                _rightWrist = wrist;
            }
        }

        private void CreateHand(bool leftSide, Transform wrist)
        {
            var side = leftSide ? "Left" : "Right";
            CreateEllipsoid(side + "Palm", wrist, new Vector3(0f, -0.074f, 0.022f), new Vector3(0.060f, 0.075f, 0.043f), _skinWarm);
            for (int i = 0; i < 4; i++)
            {
                var x = Mathf.Lerp(-0.040f, 0.040f, i / 3f);
                CreateTaperedTube(side + "Finger" + i, wrist, new Vector3(x, -0.135f, 0.050f), 0.070f, 0.010f, 0.010f, 0.007f, 0.007f, _skinWarm, new Vector3(12f, 0f, x * -220f), 0.02f);
            }

            var thumbX = leftSide ? 0.060f : -0.060f;
            CreateTaperedTube(side + "Thumb", wrist, new Vector3(thumbX, -0.095f, 0.040f), 0.070f, 0.014f, 0.012f, 0.009f, 0.008f, _skinWarm, new Vector3(30f, 0f, leftSide ? -38f : 38f), 0.02f);

            var socket = CreateJoint(side + "HandSocket", wrist, new Vector3(0f, -0.094f, 0.040f));
            socket.localRotation = Quaternion.identity;
            if (leftSide) _leftHandSocket = socket;
            else _rightHandSocket = socket;
        }

        private void BuildLeg(bool leftSide)
        {
            var sign = leftSide ? -1f : 1f;
            var hip = CreateJoint(leftSide ? "LeftHip" : "RightHip", _hips, new Vector3(sign * 0.165f, -0.080f, 0f));
            var knee = CreateLimb(leftSide ? "LeftThigh" : "RightThigh", hip, 0.455f, 0.094f, 0.071f, 0.073f, 0.057f, _pants);
            var ankle = CreateLimb(leftSide ? "LeftShin" : "RightShin", knee, 0.395f, 0.069f, 0.054f, 0.051f, 0.043f, _pants);
            CreateTaperedTube((leftSide ? "Left" : "Right") + "BootShaft", knee, new Vector3(0f, -0.300f, 0f), 0.235f, 0.082f, 0.064f, 0.070f, 0.055f, _boots, Vector3.zero, 0.05f);
            var foot = CreateJoint((leftSide ? "Left" : "Right") + "Foot", ankle, new Vector3(0f, -0.060f, 0.105f));
            CreateEllipsoid((leftSide ? "Left" : "Right") + "FootMesh", foot, Vector3.zero, new Vector3(0.086f, 0.050f, 0.176f), _boots);
            CreateEllipsoid((leftSide ? "Left" : "Right") + "BootToe", foot, new Vector3(0f, 0.008f, 0.105f), new Vector3(0.070f, 0.040f, 0.066f), _boots);

            if (leftSide)
            {
                _leftHip = hip;
                _leftKnee = knee;
                _leftAnkle = ankle;
                _leftFootMesh = foot;
            }
            else
            {
                _rightHip = hip;
                _rightKnee = knee;
                _rightAnkle = ankle;
                _rightFootMesh = foot;
            }
        }

        private void CreateRoundShield(Transform wrist)
        {
            CreateDisc("ShieldBoard", wrist, new Vector3(0f, -0.115f, 0.165f), 0.335f, 0.335f, 0.026f, _wood, new Vector3(0f, 0f, 0f));
            CreateTorus("ShieldRim", wrist, new Vector3(0f, -0.115f, 0.180f), 0.322f, 0.322f, 0.018f, _darkMetal, new Vector3(90f, 0f, 0f));
            CreateDisc("ShieldPaintInset", wrist, new Vector3(0f, -0.115f, 0.196f), 0.265f, 0.265f, 0.010f, _cloth, Vector3.zero);
            CreateEllipsoid("ShieldBoss", wrist, new Vector3(0f, -0.115f, 0.232f), new Vector3(0.115f, 0.115f, 0.070f), _metal);
            CreateStrap("ShieldHorizontalBrace", wrist, new Vector3(0f, -0.115f, 0.246f), 0.42f, 0.018f, _leather, new Vector3(90f, 0f, 90f));
            CreateStrap("ShieldGrip", wrist, new Vector3(0f, -0.115f, 0.265f), 0.18f, 0.016f, _leather, new Vector3(90f, 0f, 0f));
        }

        private Transform CreateJoint(string name, Transform parent, Vector3 localPosition)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            joint.localRotation = Quaternion.identity;
            return joint;
        }

        private Transform CreateLimb(string name, Transform parent, float length, float topX, float topZ, float bottomX, float bottomZ, Material material)
        {
            var end = CreateJoint(name + "End", parent, new Vector3(0f, -length, 0f));
            CreateTaperedTube(name, parent, Vector3.zero, length, topX, topZ, bottomX, bottomZ, material, Vector3.zero, 0.08f);
            return end;
        }

        private Transform CreateEllipsoid(string name, Transform parent, Vector3 localPosition, Vector3 radius, Material material)
        {
            return CreateMeshPart(name, parent, localPosition, Quaternion.identity, BuildEllipsoidMesh(radius, BodySegments / 2, BodySegments), material);
        }

        private Transform CreateTaperedTube(string name, Transform parent, Vector3 localPosition, float length, float topX, float topZ, float bottomX, float bottomZ, Material material, Vector3 localEuler, float bulge)
        {
            return CreateMeshPart(name, parent, localPosition, Quaternion.Euler(localEuler), BuildTaperedTubeMesh(length, topX, topZ, bottomX, bottomZ, bulge), material);
        }

        private Transform CreateStrap(string name, Transform parent, Vector3 localPosition, float length, float radius, Material material, Vector3 localEuler)
        {
            return CreateTaperedTube(name, parent, localPosition, length, radius, radius * 0.55f, radius, radius * 0.55f, material, localEuler, 0.02f);
        }

        private Transform CreateTorus(string name, Transform parent, Vector3 localPosition, float majorX, float majorZ, float minorRadius, Material material)
        {
            return CreateTorus(name, parent, localPosition, majorX, majorZ, minorRadius, material, Vector3.zero);
        }

        private Transform CreateTorus(string name, Transform parent, Vector3 localPosition, float majorX, float majorZ, float minorRadius, Material material, Vector3 localEuler)
        {
            return CreateMeshPart(name, parent, localPosition, Quaternion.Euler(localEuler), BuildTorusMesh(majorX, majorZ, minorRadius, 48, 12), material);
        }

        private Transform CreateDisc(string name, Transform parent, Vector3 localPosition, float radiusX, float radiusY, float thickness, Material material, Vector3 localEuler)
        {
            return CreateMeshPart(name, parent, localPosition, Quaternion.Euler(localEuler), BuildDiscMesh(radiusX, radiusY, thickness, 48), material);
        }

        private Transform CreateClothPanel(string name, Transform parent, Vector3 localPosition, float topWidth, float bottomWidth, float height, float curve, Material material)
        {
            return CreateClothPanel(name, parent, localPosition, topWidth, bottomWidth, height, curve, material, Vector3.zero);
        }

        private Transform CreateClothPanel(string name, Transform parent, Vector3 localPosition, float topWidth, float bottomWidth, float height, float curve, Material material, Vector3 localEuler)
        {
            return CreateMeshPart(name, parent, localPosition, Quaternion.Euler(localEuler), BuildClothPanelMesh(topWidth, bottomWidth, height, curve, 9, 9), material);
        }

        private Transform CreateMeshPart(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;

            var filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part.transform;
        }

        // ===================================================================
        // Layered biomechanical animation. Each Update we evaluate four layers
        // and compose them with weights:
        //   IDLE       — breathing, weight shift, head drift
        //   LOCOMOTION — walk / run cycle (proper heel-strike, hip drop,
        //                counter-arm swing, head bob)
        //   AIR        — jump anticipation, airborne tuck, landing recovery
        //   COMBAT     — additive overlay from PlayShieldBashPose / sword poses
        // ===================================================================

        private void AnimateFromMovement()
        {
            // ---- 1. Read motion (speed, turn, vertical velocity) ----------
            var dt = Time.deltaTime;
            var pos = transform.position;
            var fullDelta = pos - _lastPosition;
            _lastPosition = pos;

            var horizDelta = new Vector3(fullDelta.x, 0f, fullDelta.z);
            var rawSpeed = dt > 0.0001f ? horizDelta.magnitude / dt : 0f;
            var smoothing = 1f - Mathf.Exp(-speedSmoothing * dt);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, smoothing);

            var rawVertical = dt > 0.0001f ? fullDelta.y / dt : 0f;
            _smoothedVertical = Mathf.Lerp(_smoothedVertical, rawVertical, smoothing);
            _verticalVelocity = _smoothedVertical;

            var signedTurn = Vector3.SignedAngle(_lastForward, transform.forward, Vector3.up);
            _lastForward = transform.forward;
            var turnSmoothingAmount = 1f - Mathf.Exp(-turnSmoothing * dt);
            _turnAmount = Mathf.Lerp(_turnAmount, Mathf.Clamp(signedTurn * 0.055f, -1f, 1f), turnSmoothingAmount);

            // ---- 2. Detect grounded vs airborne ---------------------------
            // We don't have direct access to CharacterController here without
            // coupling, so we infer from sustained vertical motion: |vy| > 0.6
            // for > 0.05s = airborne.
            var airborne = Mathf.Abs(_verticalVelocity) > 0.6f && dt > 0f;
            if (airborne)
            {
                _airTime += dt;
            }
            else
            {
                if (_wasAirborneLastFrame && _airTime > 0.10f) _landRecoveryT = 1f;
                _airTime = 0f;
            }
            _wasAirborneLastFrame = airborne;
            _landRecoveryT = Mathf.Max(0f, _landRecoveryT - dt / Mathf.Max(landRecoverySeconds, 0.01f));

            // ---- 3. Normalized speed + cycle phase ------------------------
            var moving = _smoothedSpeed > walkThreshold;
            var normalizedSpeed = Mathf.Clamp01(_smoothedSpeed / Mathf.Max(runThreshold, 0.01f));
            var runBlend = Mathf.SmoothStep(0f, 1f, normalizedSpeed);
            var locomotionWeight = moving && !airborne ? Mathf.SmoothStep(0f, 1f, _smoothedSpeed * 4f) : 0f;
            locomotionWeight = Mathf.Clamp01(locomotionWeight);
            var idleWeight = (1f - locomotionWeight) * (airborne ? 0.0f : 1f);
            var rate = Mathf.Lerp(walkCycleRate, runCycleRate, runBlend);
            if (moving) _cycle += dt * rate;
            else        _cycle = Mathf.Lerp(_cycle, 0f, smoothing);

            // ---- 4. Compute pose contributions ----------------------------
            var hipPosLocal = new Vector3(0f, 0.92f, 0f);
            var hipRotEuler = new Vector3(0f, _turnAmount * 4f, 0f);
            var torsoEuler  = new Vector3(0f, _turnAmount * 7f, 0f);
            var chestEuler  = new Vector3(0f, _turnAmount * 7f, 0f);
            var headEuler   = new Vector3(0f, -_turnAmount * 8f, 0f);

            var leftShoulderE  = new Vector3(8f,  0f, -11f);
            var leftElbowE     = new Vector3(12f, 0f, 0f);
            var leftWristE     = Vector3.zero;
            var rightShoulderE = new Vector3(12f, -4f, 12f);
            var rightElbowE    = new Vector3(24f, 0f, 0f);
            var rightWristE    = new Vector3(-3f, 0f, 0f);

            // ---- IDLE LAYER ---- breathing + weight-shift + head drift
            if (idleWeight > 0.001f)
            {
                var t = Time.time;
                var breath = Mathf.Sin(t * idleBreathRate * 2f * Mathf.PI);  // -1..1
                var sway   = Mathf.Sin(t * idleSwayRate   * 2f * Mathf.PI);

                hipPosLocal.x += sway * idleSwayAmount * idleWeight;
                hipPosLocal.y += (breath + 1f) * 0.5f * idleBreathDepth * idleWeight;
                hipRotEuler.z += sway * 0.6f * idleWeight;
                chestEuler.x  += -breath * 1.4f * idleWeight;     // chest expands on inhale
                chestEuler.z  += sway * 0.8f * idleWeight;
                torsoEuler.z  += sway * 0.4f * idleWeight;

                // arms drift very slightly with breath
                leftShoulderE.z  += -breath * 0.6f * idleWeight;
                rightShoulderE.z += breath  * 0.6f * idleWeight;

                headEuler.y += sway * idleHeadBobAmount * idleWeight * 0.3f;
                headEuler.x += -breath * 0.3f * idleWeight;
            }

            // ---- LOCOMOTION LAYER ---- walk + run cycle
            if (locomotionWeight > 0.001f)
            {
                var leftPhase  = _cycle;
                var rightPhase = _cycle + Mathf.PI;
                var leftSwing  = Mathf.Sin(leftPhase);
                var rightSwing = Mathf.Sin(rightPhase);
                var leftLift   = Mathf.Clamp01(Mathf.Sin(leftPhase  - 0.15f));
                var rightLift  = Mathf.Clamp01(Mathf.Sin(rightPhase - 0.15f));

                // Hip drop on supporting (planted) leg + counter-rotation around vertical.
                // When the right foot is planted (right phase is in stance, sin ≈ -1 to 0),
                // the hip drops on the right and rotates so the left side leads forward.
                var stancePhase = Mathf.Cos(_cycle);                    // -1 = right plant, +1 = left plant
                var hipDrop = stancePhase * Mathf.Lerp(0.012f, 0.028f, runBlend) * locomotionWeight;
                var hipYaw  = -Mathf.Sin(_cycle) * Mathf.Lerp(3.2f, 6.5f, runBlend);   // pelvis rotation
                var chestYaw = -hipYaw * 0.7f;                                          // chest counter-rotates

                var stride       = Mathf.Lerp(22f, 42f, runBlend);
                var armStride    = Mathf.Lerp(28f, 56f, runBlend);
                var kneeBend     = Mathf.Lerp(20f, 48f, runBlend);
                var footPitch    = Mathf.Lerp( 8f, 19f, runBlend);
                var bobHeight    = Mathf.Lerp(0.026f, 0.065f, runBlend);
                var forwardLean  = Mathf.Lerp(  3f, 11f, runBlend);

                // Vertical bounce: peaks at mid-stance (when stancePhase is 0).
                var bounce = Mathf.Abs(Mathf.Cos(_cycle)) * bobHeight;
                var sideSway = Mathf.Sin(_cycle) * Mathf.Lerp(0.014f, 0.028f, runBlend);

                hipPosLocal.x += sideSway * locomotionWeight;
                hipPosLocal.y += bounce * locomotionWeight - Mathf.Abs(hipDrop) * 0.5f;

                hipRotEuler.x += locomotionWeight * Mathf.Abs(leftSwing) * 1.2f;
                hipRotEuler.y += hipYaw * locomotionWeight;
                hipRotEuler.z += hipDrop * 60f;       // drop expressed as roll

                torsoEuler.x  += forwardLean * locomotionWeight;
                torsoEuler.y  += chestYaw * locomotionWeight;
                torsoEuler.z  += rightSwing * 2.0f * locomotionWeight;

                chestEuler.x  += forwardLean * 0.45f * locomotionWeight;
                chestEuler.y  += chestYaw * locomotionWeight;
                chestEuler.z  += leftSwing * 3.0f * locomotionWeight;

                // Head bobs DOWN on heel-strike (when supporting leg starts taking weight)
                var headBob = -Mathf.Abs(leftSwing) * 1.6f * locomotionWeight;
                headEuler.x += headBob;
                headEuler.z += rightSwing * 0.8f * locomotionWeight;

                // Counter-arm swing: arm opposite to leg
                leftShoulderE  += new Vector3(rightSwing * armStride, 0f, -leftSwing  * 4f) * locomotionWeight;
                leftElbowE     += new Vector3(8f + leftLift * 22f, 0f, 0f) * locomotionWeight;
                leftWristE     += new Vector3(-rightSwing * 8f, 0f, 0f) * locomotionWeight;

                rightShoulderE += new Vector3(leftSwing * armStride, 0f, rightSwing * 4f) * locomotionWeight;
                rightElbowE    += new Vector3(8f + rightLift * 22f, 0f, 0f) * locomotionWeight;
                rightWristE    += new Vector3(-leftSwing * 8f, 0f, 0f) * locomotionWeight;

                AnimateLegLayered(_leftHip,  _leftKnee,  _leftAnkle,  _leftFootMesh,  leftSwing,  leftLift,  stride, kneeBend, footPitch, locomotionWeight);
                AnimateLegLayered(_rightHip, _rightKnee, _rightAnkle, _rightFootMesh, rightSwing, rightLift, stride, kneeBend, footPitch, locomotionWeight);
            }
            else
            {
                // Idle leg pose
                AnimateLegLayered(_leftHip,  _leftKnee,  _leftAnkle,  _leftFootMesh,  0f, 0f, 0f, 0f, 0f, 0f);
                AnimateLegLayered(_rightHip, _rightKnee, _rightAnkle, _rightFootMesh, 0f, 0f, 0f, 0f, 0f, 0f);
            }

            // ---- AIR LAYER ---- airborne tuck + landing squash
            if (airborne)
            {
                var rising = _verticalVelocity > 0f;
                var airBlend = Mathf.Clamp01(_airTime / 0.30f);

                // Knees tuck up, arms slightly forward for balance
                hipPosLocal.y -= 0.04f * airBlend;
                torsoEuler.x  += rising ?  6f * airBlend : -10f * airBlend;       // lean forward rising, arch back falling
                chestEuler.x  += rising ?  3f * airBlend : -6f  * airBlend;
                leftShoulderE  += new Vector3(-30f * airBlend, 0f, -8f * airBlend);
                rightShoulderE += new Vector3(-26f * airBlend, 0f,  8f * airBlend);
                leftElbowE  += new Vector3(45f * airBlend, 0f, 0f);
                rightElbowE += new Vector3(45f * airBlend, 0f, 0f);
            }
            if (_landRecoveryT > 0.001f)
            {
                // Brief crouch on landing
                var amount = Mathf.SmoothStep(0f, 1f, _landRecoveryT);
                hipPosLocal.y -= 0.06f * amount;
                torsoEuler.x  +=  6f * amount;
                chestEuler.x  +=  4f * amount;
            }

            // ---- LOOK LAYER ---- head tracks camera pitch a bit
            if (Camera.main != null)
            {
                var camFwd = Camera.main.transform.forward;
                var localCamFwd = transform.InverseTransformDirection(camFwd);
                var pitchTarget = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localCamFwd.y, -1f, 1f)) * Mathf.Rad2Deg, headPitchMin, headPitchMax);
                _smoothedHeadPitch = Mathf.Lerp(_smoothedHeadPitch, pitchTarget, 1f - Mathf.Exp(-9f * dt));
                headEuler.x += _smoothedHeadPitch * headFollowCamera;
            }

            // ---- 5. Apply composed pose -----------------------------------
            SetLocalPosition(_hips, hipPosLocal);
            SetLocalRotation(_hips,  Quaternion.Euler(hipRotEuler));
            SetLocalRotation(_torso, Quaternion.Euler(torsoEuler));
            SetLocalRotation(_chest, Quaternion.Euler(chestEuler));
            SetLocalRotation(_head,  Quaternion.Euler(headEuler));

            SetLocalRotation(_leftShoulder,  Quaternion.Euler(leftShoulderE));
            SetLocalRotation(_leftElbow,     Quaternion.Euler(leftElbowE));
            SetLocalRotation(_leftWrist,     Quaternion.Euler(leftWristE));
            SetLocalRotation(_rightShoulder, Quaternion.Euler(rightShoulderE));
            SetLocalRotation(_rightElbow,    Quaternion.Euler(rightElbowE));
            SetLocalRotation(_rightWrist,    Quaternion.Euler(rightWristE));

            ApplyAttackOverlay();
        }

        // Layered leg evaluation. Weight scales the entire pose so we lerp
        // smoothly into idle when speed drops.
        private static void AnimateLegLayered(Transform hip, Transform knee, Transform ankle, Transform foot,
            float swing, float lift, float stride, float kneeBend, float footPitch, float weight)
        {
            var hipP   = swing * stride * weight;
            var kneeP  = Mathf.Lerp(2f, 4f + lift * kneeBend, weight);
            var ankleP = (-swing * footPitch - lift * 5f) * weight;
            var footY  = -0.060f + lift * 0.045f * weight;
            var footZ  = 0.105f + swing * 0.018f * weight;
            SetLocalRotation(hip,   Quaternion.Euler(hipP,   0f, 0f));
            SetLocalRotation(knee,  Quaternion.Euler(kneeP,  0f, 0f));
            SetLocalRotation(ankle, Quaternion.Euler(ankleP, 0f, 0f));
            SetLocalPosition(foot,  new Vector3(0f, footY, footZ));
        }

        private void ApplyAttackOverlay()
        {
            if (_attackWeight <= 0.001f) return;

            BlendLocalRotation(_torso, Quaternion.Euler(_attackTorsoEuler), _attackWeight * 0.55f);
            BlendLocalRotation(_chest, Quaternion.Euler(_attackTorsoEuler * 0.75f), _attackWeight * 0.45f);
            BlendLocalRotation(_rightShoulder, Quaternion.Euler(_attackRightShoulderEuler), _attackWeight);
            BlendLocalRotation(_rightElbow, Quaternion.Euler(_attackRightElbowEuler), _attackWeight);
            BlendLocalRotation(_rightWrist, Quaternion.Euler(_attackRightWristEuler), _attackWeight);
            BlendLocalRotation(_leftShoulder, Quaternion.Euler(_attackLeftShoulderEuler), _attackWeight * 0.55f);
        }

        private IEnumerator AnimateSwordAttackPose(bool heavy, int comboStep)
        {
            var leftSlash = comboStep == 1;

            // ANTICIPATION (briefly bias body opposite to where the attack will go)
            // gives the swing weight — the brain reads it as commitment.
            var antiTorso    = heavy ? new Vector3(-1f, -8f, -2f)  : new Vector3(0f, -4f, -1f);
            var antiShoulder = heavy ? new Vector3(-12f, -8f, 8f)  : new Vector3(-6f, -4f, 4f);
            var antiElbow    = heavy ? new Vector3(28f, 0f, 0f)    : new Vector3(20f, 0f, 0f);
            var antiWrist    = Vector3.zero;
            var antiShield   = new Vector3(8f, 0f, -12f);

            // WINDUP (deeper than anticipation — sword goes back behind shoulder)
            var windupTorso    = heavy ? new Vector3(-2f, -28f, -6f) : new Vector3(-1f, -18f, -4f);
            var windupShoulder = heavy ? new Vector3(-92f, -28f, 38f) : new Vector3(-66f, -18f, 28f);
            var windupElbow    = heavy ? new Vector3(75f, 0f, 0f)    : new Vector3(55f, 0f, 0f);
            var windupWrist    = heavy ? new Vector3(3f, -14f, -22f) : new Vector3(1f, -10f, -15f);
            var windupShield   = new Vector3(18f, 0f, -22f);

            // STRIKE (explosive forward) — short duration so it snaps
            var slashTorso    = heavy ? new Vector3(7f, 30f, 9f) : new Vector3(5f, leftSlash ? 22f : 14f, leftSlash ? 7f : 4f);
            var slashShoulder = heavy ? new Vector3(-22f, 42f, -32f) : new Vector3(-28f, leftSlash ? 36f : 25f, leftSlash ? -26f : -18f);
            var slashElbow    = heavy ? new Vector3(20f, 0f, 0f) : new Vector3(22f, 0f, 0f);
            var slashWrist    = heavy ? new Vector3(-10f, 10f, 24f) : new Vector3(-5f, 6f, leftSlash ? 22f : 15f);
            var slashShield   = heavy ? new Vector3(28f, 0f, -22f) : new Vector3(18f, 0f, -16f);

            // FOLLOW-THROUGH (a beat past the strike — overshoot)
            var followTorso    = slashTorso * 1.10f;
            var followShoulder = slashShoulder + new Vector3(8f, 4f, -4f);
            var followElbow    = slashElbow + new Vector3(-6f, 0f, 0f);
            var followWrist    = slashWrist + new Vector3(0f, 0f, 4f);
            var followShield   = slashShield;

            // RECOVERY back to combat idle
            var idleTorso    = Vector3.zero;
            var idleShoulder = new Vector3(12f, -4f, 12f);
            var idleElbow    = new Vector3(24f, 0f, 0f);
            var idleWrist    = new Vector3(-3f, 0f, 0f);
            var idleShield   = new Vector3(8f, 0f, -11f);

            // Phase timing — anticipation is short, windup is the longest, strike is
            // the shortest (snap), follow-through holds, recovery eases out.
            yield return BlendAttackPose(antiTorso,    antiShoulder,    antiElbow,    antiWrist,    antiShield,    0.7f, 0.06f);
            yield return BlendAttackPose(windupTorso,  windupShoulder,  windupElbow,  windupWrist,  windupShield,  1.0f, heavy ? 0.16f : 0.10f);
            yield return BlendAttackPose(slashTorso,   slashShoulder,   slashElbow,   slashWrist,   slashShield,   1.0f, heavy ? 0.07f : 0.05f);
            yield return BlendAttackPose(followTorso,  followShoulder,  followElbow,  followWrist,  followShield,  0.85f, 0.06f);
            yield return BlendAttackPose(idleTorso,    idleShoulder,    idleElbow,    idleWrist,    idleShield,    0.0f, heavy ? 0.30f : 0.22f);

            _attackWeight = 0f;
            _attackRoutine = null;
        }

        private IEnumerator AnimateShieldBashPose()
        {
            // 4-phase rewrite. Original poses were ~20° max which read as a flinch,
            // not a bash. Now: real windup (torso back, shield tucked close), an
            // explosive 60ms strike (shield punched fully extended, body counter-rotated),
            // a held impact frame, and a 2-stage recovery with a small overshoot to
            // give the motion follow-through weight.

            // Phase 1 — Windup (0.16s, ease-in via existing smoothstep)
            //   torso leans back-and-left, shield shoulder pulls in to chest
            yield return BlendAttackPose(
                torso:         new Vector3(-14f, -28f, -10f),
                shoulder:      new Vector3( -8f,  -2f,   5f),
                elbow:         new Vector3( 45f,   0f,   0f),
                wrist:         new Vector3( -2f,   0f,  -4f),
                leftShoulder:  new Vector3(-18f,  32f, -22f),
                targetWeight: 1f, duration: 0.16f);

            // Phase 2 — Explosive strike (0.06s — should feel violent)
            //   torso lunges forward+right, right arm thrown back as counter-balance,
            //   left shoulder fully extended (-95, -8, -88) for a true punching shield
            yield return BlendAttackPose(
                torso:         new Vector3( 18f,  28f,   8f),
                shoulder:      new Vector3(-32f,  18f, -10f),
                elbow:         new Vector3( 20f,   0f,   0f),
                wrist:         new Vector3( -1f,   0f,   8f),
                leftShoulder:  new Vector3(-95f,  -8f, -88f),
                targetWeight: 1f, duration: 0.06f);

            // Phase 3 — Hold impact frame (0.06s)
            //   no movement; gives the impact visual weight before recovery starts
            yield return new WaitForSeconds(0.06f);

            // Phase 4a — Recovery overshoot (0.10s)
            //   slight pull-back past idle so the return feels springy, not robotic
            yield return BlendAttackPose(
                torso:         new Vector3(  2f,   4f,   1f),
                shoulder:      new Vector3(  8f,  -2f,   8f),
                elbow:         new Vector3( 20f,   0f,   0f),
                wrist:         new Vector3( -3f,   0f,   0f),
                leftShoulder:  new Vector3(  2f,   0f, -16f),
                targetWeight: 0.4f, duration: 0.10f);

            // Phase 4b — Settle to combat idle (0.20s)
            yield return BlendAttackPose(
                torso:         Vector3.zero,
                shoulder:      new Vector3( 12f, -4f, 12f),
                elbow:         new Vector3( 24f,  0f,  0f),
                wrist:         new Vector3( -3f,  0f,  0f),
                leftShoulder:  new Vector3(  8f,  0f, -11f),
                targetWeight: 0f, duration: 0.20f);

            _attackWeight = 0f;
            _attackRoutine = null;
        }

        private IEnumerator AnimateSkillAttackPose(bool axe)
        {
            var windupTorso = axe ? new Vector3(-8f, -34f, -7f) : new Vector3(-5f, -28f, -5f);
            var windupShoulder = axe ? new Vector3(-98f, -28f, 42f) : new Vector3(-84f, -24f, 36f);
            var slashTorso = axe ? new Vector3(11f, 38f, 9f) : new Vector3(8f, 35f, 8f);
            var slashShoulder = axe ? new Vector3(-8f, 48f, -38f) : new Vector3(-16f, 45f, -34f);

            yield return BlendAttackPose(windupTorso, windupShoulder, new Vector3(78f, 0f, 0f), new Vector3(5f, -18f, -26f), new Vector3(18f, 0f, -18f), 1f, 0.14f);
            yield return BlendAttackPose(slashTorso, slashShoulder, new Vector3(20f, 0f, 0f), new Vector3(-10f, 12f, 28f), new Vector3(24f, 0f, -18f), 1f, 0.14f);
            yield return BlendAttackPose(Vector3.zero, new Vector3(12f, -4f, 12f), new Vector3(24f, 0f, 0f), new Vector3(-3f, 0f, 0f), new Vector3(8f, 0f, -11f), 0f, 0.26f);

            _attackWeight = 0f;
            _attackRoutine = null;
        }

        private IEnumerator BlendAttackPose(Vector3 torso, Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3 leftShoulder, float targetWeight, float duration)
        {
            var startTorso = _attackTorsoEuler;
            var startShoulder = _attackRightShoulderEuler;
            var startElbow = _attackRightElbowEuler;
            var startWrist = _attackRightWristEuler;
            var startLeftShoulder = _attackLeftShoulderEuler;
            var startWeight = _attackWeight;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.001f));
                t = t * t * (3f - 2f * t);
                _attackTorsoEuler = Vector3.Lerp(startTorso, torso, t);
                _attackRightShoulderEuler = Vector3.Lerp(startShoulder, shoulder, t);
                _attackRightElbowEuler = Vector3.Lerp(startElbow, elbow, t);
                _attackRightWristEuler = Vector3.Lerp(startWrist, wrist, t);
                _attackLeftShoulderEuler = Vector3.Lerp(startLeftShoulder, leftShoulder, t);
                _attackWeight = Mathf.Lerp(startWeight, targetWeight, t);
                yield return null;
            }

            _attackTorsoEuler = torso;
            _attackRightShoulderEuler = shoulder;
            _attackRightElbowEuler = elbow;
            _attackRightWristEuler = wrist;
            _attackLeftShoulderEuler = leftShoulder;
            _attackWeight = targetWeight;
        }

        private void CreateMaterials()
        {
            var appearance = _appearance.Sanitized();
            _appearance = appearance;

            _skin = CreateMaterial("VikingSkin", appearance.skinColor, 0.46f, 0f);
            _skinWarm = CreateMaterial("VikingSkinWarm", Color.Lerp(appearance.skinColor, Color.white, 0.11f), 0.48f, 0f);
            _tunic = CreateMaterial("VikingTunicGreen", appearance.tunicColor, 0.54f, 0f);
            _pants = CreateMaterial("VikingWoolPants", appearance.pantsColor, 0.50f, 0f);
            _boots = CreateMaterial("VikingBoots", new Color(0.11f, 0.065f, 0.040f), 0.26f, 0f);
            _leather = CreateMaterial("VikingLeather", new Color(0.33f, 0.19f, 0.105f), 0.30f, 0f);
            _wood = CreateMaterial("VikingShieldWood", new Color(0.50f, 0.31f, 0.16f), 0.36f, 0f);
            _fur = CreateMaterial("VikingFur", new Color(0.48f, 0.44f, 0.38f), 0.76f, 0f);
            _hair = CreateMaterial("VikingHair", appearance.hairColor, 0.32f, 0f);
            _metal = CreateMaterial("VikingHelmetIron", new Color(0.54f, 0.56f, 0.55f), 0.34f, 0.35f);
            _darkMetal = CreateMaterial("VikingDarkIron", new Color(0.19f, 0.21f, 0.21f), 0.40f, 0.25f);
            _cloth = CreateMaterial("VikingBlueCloth", appearance.cloakColor, 0.50f, 0f);
            _eye = CreateMaterial("VikingEyes", new Color(0.025f, 0.020f, 0.016f), 0.08f, 0f);
        }

        private static Mesh BuildEllipsoidMesh(Vector3 radius, int latitude, int longitude)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int lat = 0; lat <= latitude; lat++)
            {
                var v = lat / (float)latitude;
                var phi = v * Mathf.PI;
                var sinPhi = Mathf.Sin(phi);
                var cosPhi = Mathf.Cos(phi);

                for (int lon = 0; lon <= longitude; lon++)
                {
                    var u = lon / (float)longitude;
                    var theta = u * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        Mathf.Cos(theta) * sinPhi * radius.x,
                        cosPhi * radius.y,
                        Mathf.Sin(theta) * sinPhi * radius.z));
                }
            }

            var row = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    var a = lat * row + lon;
                    var b = a + row;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            return FinalizeMesh("Ellipsoid", vertices, triangles);
        }

        private static Mesh BuildTaperedTubeMesh(float length, float topX, float topZ, float bottomX, float bottomZ, float bulge)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int rings = 14;

            for (int ring = 0; ring <= rings; ring++)
            {
                var t = ring / (float)rings;
                var y = -t * length;
                var swell = 1f + Mathf.Sin(t * Mathf.PI) * bulge;
                var rx = Mathf.Lerp(topX, bottomX, t) * swell;
                var rz = Mathf.Lerp(topZ, bottomZ, t) * swell;

                for (int i = 0; i < LimbSegments; i++)
                {
                    var a = i / (float)LimbSegments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * rx, y, Mathf.Sin(a) * rz));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                var start = ring * LimbSegments;
                var next = (ring + 1) * LimbSegments;
                for (int i = 0; i < LimbSegments; i++)
                {
                    var ni = (i + 1) % LimbSegments;
                    triangles.Add(start + i);
                    triangles.Add(next + i);
                    triangles.Add(start + ni);
                    triangles.Add(start + ni);
                    triangles.Add(next + i);
                    triangles.Add(next + ni);
                }
            }

            var topCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            var bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -length, 0f));

            for (int i = 0; i < LimbSegments; i++)
            {
                var ni = (i + 1) % LimbSegments;
                triangles.Add(topCenter);
                triangles.Add(ni);
                triangles.Add(i);

                var bottomStart = rings * LimbSegments;
                triangles.Add(bottomCenter);
                triangles.Add(bottomStart + i);
                triangles.Add(bottomStart + ni);
            }

            return FinalizeMesh("TaperedTube", vertices, triangles);
        }

        private static Mesh BuildTorusMesh(float majorX, float majorZ, float minor, int segments, int tubeSegments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                var u = i / (float)segments * Mathf.PI * 2f;
                var cu = Mathf.Cos(u);
                var su = Mathf.Sin(u);

                for (int j = 0; j <= tubeSegments; j++)
                {
                    var v = j / (float)tubeSegments * Mathf.PI * 2f;
                    var cv = Mathf.Cos(v);
                    var sv = Mathf.Sin(v);
                    vertices.Add(new Vector3((majorX + minor * cv) * cu, minor * sv, (majorZ + minor * cv) * su));
                }
            }

            var row = tubeSegments + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    var a = i * row + j;
                    var b = (i + 1) * row + j;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            return FinalizeMesh("Torus", vertices, triangles);
        }

        private static Mesh BuildDiscMesh(float radiusX, float radiusY, float thickness, int segments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var half = thickness * 0.5f;

            for (int side = 0; side < 2; side++)
            {
                var z = side == 0 ? -half : half;
                vertices.Add(new Vector3(0f, 0f, z));
                for (int i = 0; i < segments; i++)
                {
                    var a = i / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY, z));
                }
            }

            for (int i = 0; i < segments; i++)
            {
                var ni = (i + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + i);
                triangles.Add(1 + ni);

                var offset = segments + 1;
                triangles.Add(offset);
                triangles.Add(offset + 1 + ni);
                triangles.Add(offset + 1 + i);

                triangles.Add(1 + i);
                triangles.Add(offset + 1 + i);
                triangles.Add(1 + ni);
                triangles.Add(1 + ni);
                triangles.Add(offset + 1 + i);
                triangles.Add(offset + 1 + ni);
            }

            return FinalizeMesh("Disc", vertices, triangles);
        }

        private static Mesh BuildClothPanelMesh(float topWidth, float bottomWidth, float height, float curve, int xSegments, int ySegments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int y = 0; y <= ySegments; y++)
            {
                var ty = y / (float)ySegments;
                var width = Mathf.Lerp(topWidth, bottomWidth, ty);
                var rowY = -ty * height + Mathf.Sin(ty * Mathf.PI) * 0.020f;

                for (int x = 0; x <= xSegments; x++)
                {
                    var tx = x / (float)xSegments;
                    var localX = (tx - 0.5f) * width;
                    var z = curve * Mathf.Sin(tx * Mathf.PI) + Mathf.Sin((tx + ty) * Mathf.PI * 2f) * 0.006f;
                    vertices.Add(new Vector3(localX, rowY, z));
                }
            }

            var row = xSegments + 1;
            for (int y = 0; y < ySegments; y++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    var a = y * row + x;
                    var b = a + row;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                    triangles.Add(b);

                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            return FinalizeMesh("ClothPanel", vertices, triangles);
        }

        private static Mesh FinalizeMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void SetLocalPosition(Transform target, Vector3 position)
        {
            if (target != null) target.localPosition = position;
        }

        private static void SetLocalRotation(Transform target, Quaternion rotation)
        {
            if (target != null) target.localRotation = rotation;
        }

        private static void BlendLocalRotation(Transform target, Quaternion targetRotation, float weight)
        {
            if (target != null) target.localRotation = Quaternion.Slerp(target.localRotation, targetRotation, Mathf.Clamp01(weight));
        }

        private static void SetChildrenActiveByName(Transform root, string token, bool active)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                SetChildrenActiveByName(root.GetChild(i), token, active);
            }

            if (root.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                root.gameObject.SetActive(active);
            }
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            return ProceduralArt.CreateLitMaterial(name, color, SurfaceForMaterial(name), smoothness, metallic);
        }

        private static ArtSurface SurfaceForMaterial(string name)
        {
            if (name.Contains("Skin")) return ArtSurface.Skin;
            if (name.Contains("Tunic") || name.Contains("Pants") || name.Contains("Cloth")) return ArtSurface.Wool;
            if (name.Contains("Leather") || name.Contains("Boot")) return ArtSurface.Leather;
            if (name.Contains("Wood")) return ArtSurface.Wood;
            if (name.Contains("Fur")) return ArtSurface.Fur;
            if (name.Contains("Hair")) return ArtSurface.Hair;
            if (name.Contains("DarkIron")) return ArtSurface.DarkMetal;
            if (name.Contains("Iron") || name.Contains("Helmet") || name.Contains("Metal")) return ArtSurface.Metal;
            if (name.Contains("Eye")) return ArtSurface.Plain;
            return ArtSurface.Plain;
        }
    }
}
