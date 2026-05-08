using Mirror;
using Steading.Combat;
using UnityEngine;

namespace Steading.Player
{
    // Drives a Unity Animator from gameplay events. Replaces PlayerVisualAnimator's
    // procedural pose blending. Sits on the same GameObject as PlayerController +
    // an Animator that uses PlayerAnimator.controller.
    //
    // Animator parameters used:
    //   Speed       float    — horizontal speed (m/s), drives locomotion blend tree
    //   Grounded    bool     — false during airborne tuck/landing
    //   VerticalVel float    — vy, used for jump/fall variant in blend tree
    //   Crouch      bool     — held while crouch is active
    //   Block       bool     — held while RMB block is active
    //   Slash       trigger  — sword swing (combo step 1)
    //   Combo       trigger  — sword swing (combo step 2)
    //   ShieldRush  trigger  — block-held + LMB tap, driver of dash
    //   PowerBash   trigger  — crouch + LMB release after charge
    //   HitReact    trigger  — fired from Health.Damaged
    //   Die         trigger  — fired from Health.Died
    //
    // The PlayerVisualAnimator API surface is preserved so existing callers
    // (PlayerAttack.RpcPlayWeaponSwing/RpcPlayShieldBash/RpcPlayWeaponSkill) work
    // unchanged — the bridge implements the same method names and forwards them
    // to Animator triggers.
    public class PlayerAnimatorBridge : NetworkBehaviour
    {
        // ------------------------------------------------- Animator hashes
        private static readonly int HashSpeed       = Animator.StringToHash("Speed");
        private static readonly int HashGrounded    = Animator.StringToHash("Grounded");
        private static readonly int HashVerticalVel = Animator.StringToHash("VerticalVel");
        private static readonly int HashCrouch      = Animator.StringToHash("Crouch");
        private static readonly int HashBlock       = Animator.StringToHash("Block");
        private static readonly int HashSlash       = Animator.StringToHash("Slash");
        private static readonly int HashCombo       = Animator.StringToHash("Combo");
        private static readonly int HashShieldRush  = Animator.StringToHash("ShieldRush");
        private static readonly int HashPowerBash   = Animator.StringToHash("PowerBash");
        private static readonly int HashMine        = Animator.StringToHash("Mine");
        private static readonly int HashChop        = Animator.StringToHash("Chop");
        private static readonly int HashHitReact    = Animator.StringToHash("HitReact");
        private static readonly int HashDie         = Animator.StringToHash("Die");

        [Header("Sources")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Health health;

        [Header("Tuning")]
        [SerializeField] private float speedSmoothing = 12f;

        private Animator _animator;
        private float _smoothedSpeed;
        private Vector3 _lastPosition;
        private bool _wired;

        // ------------------------------------------------- API surface preserved from
        // ------------------------------------------------- the old PlayerVisualAnimator
        // so PlayerAttack RPCs keep working without edits.
        // C# `?.` doesn't trigger Unity's overloaded `==` (fake-null), so we
        // route through SafeBoneSearch which checks both. Falls back to the
        // Animator's avatar bone lookup when the literal name isn't found,
        // and finally to a hand transform on the player itself.
        public Transform RightHandSocket => ResolveHand(HumanBodyBones.RightHand, "RightHand", "mixamorig:RightHand");
        public Transform LeftHandSocket  => ResolveHand(HumanBodyBones.LeftHand,  "LeftHand",  "mixamorig:LeftHand");

        private Transform ResolveHand(HumanBodyBones bone, params string[] literalNames)
        {
            EnsureAnimator();
            if (_animator == null) return transform;          // never null — caller can safely .position
            if (_animator.isHuman)
            {
                var t = _animator.GetBoneTransform(bone);
                if (t != null) return t;
            }
            for (int i = 0; i < literalNames.Length; i++)
            {
                var t = FindBoneRecursive(_animator.transform, literalNames[i]);
                if (t != null) return t;
            }
            return _animator.transform;
        }

        private void EnsureAnimator()
        {
            // Unity-aware null check: `_animator == null` triggers the engine's
            // overloaded == (catches destroyed components); `?:` does not.
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
            }
        }
        public float CurrentSpeed => _smoothedSpeed;

        public void EnsureRig() { /* no-op: rig comes from imported FBX */ }
        public void ApplyCustomization(CharacterCustomization c) { /* TBD: blend shape / material swap */ }

        public void PlaySwordAttackPose(bool heavy, int comboStep)
        {
            if (_animator == null) return;
            _animator.SetTrigger(comboStep == 1 ? HashCombo : HashSlash);
        }

        public void PlayShieldBashPose() { if (_animator != null) _animator.SetTrigger(HashShieldRush); }
        public void PlayShieldRushPose() { if (_animator != null) _animator.SetTrigger(HashShieldRush); }
        public void PlayPowerBashPose()  { if (_animator != null) _animator.SetTrigger(HashPowerBash); }
        public void PlayMinePose()       { if (_animator != null) _animator.SetTrigger(HashMine); }
        public void PlayChopPose()       { if (_animator != null) _animator.SetTrigger(HashChop); }

        public void PlaySkillAttackPose(bool axe)
        {
            if (_animator == null) return;
            _animator.SetTrigger(axe ? HashCombo : HashSlash);
        }

        // ------------------------------------------------- Lifecycle

        private void Awake()
        {
            // The Animator lives on the imported VikingHero child (M1Setup spawns
            // the FBX as a child of the player GameObject). Find it via either
            // direct or child lookup so the bridge survives both layouts.
            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (_animator != null) _animator.applyRootMotion = false;

            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (playerInput        == null) playerInput        = GetComponent<PlayerInput>();
            if (health             == null) health             = GetComponent<Health>();

            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            WireHealthEvents();
        }

        private void OnDisable()
        {
            UnwireHealthEvents();
        }

        private void WireHealthEvents()
        {
            if (_wired || health == null) return;
            health.Damaged += OnDamaged;
            health.Died    += OnDied;
            _wired = true;
        }

        private void UnwireHealthEvents()
        {
            if (!_wired || health == null) return;
            health.Damaged -= OnDamaged;
            health.Died    -= OnDied;
            _wired = false;
        }

        // ------------------------------------------------- Per-frame

        private void Update()
        {
            if (_animator == null) return;

            // Defensive: any FBX clip with embedded root motion can re-enable
            // applyRootMotion mid-game even after our once-on-import setting.
            // Keep it false every frame so the animator never moves the body
            // out from under the CharacterController (root cause of "slide
            // back to start" after combo).
            if (_animator.applyRootMotion) _animator.applyRootMotion = false;

            var dt = Time.deltaTime;
            var pos = transform.position;
            var dx = (pos - _lastPosition);
            _lastPosition = pos;

            var horiz = new Vector3(dx.x, 0f, dx.z);
            var rawSpeed = dt > 0.0001f ? horiz.magnitude / dt : 0f;
            var t = 1f - Mathf.Exp(-speedSmoothing * dt);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, t);

            _animator.SetFloat(HashSpeed, _smoothedSpeed);
            _animator.SetFloat(HashVerticalVel, dt > 0.0001f ? dx.y / dt : 0f);
            _animator.SetBool(HashGrounded, characterController == null || characterController.isGrounded);
            if (playerInput != null)
            {
                _animator.SetBool(HashCrouch, playerInput.CrouchHeld);
                _animator.SetBool(HashBlock,  playerInput.BlockHeld);
            }
        }

        // ------------------------------------------------- Health hooks

        private void OnDamaged(DamageInfo info)
        {
            if (_animator != null) _animator.SetTrigger(HashHitReact);
        }

        private void OnDied()
        {
            if (_animator != null) _animator.SetTrigger(HashDie);
        }

        // ------------------------------------------------- Helpers

        private static Transform FindBoneRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindBoneRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
