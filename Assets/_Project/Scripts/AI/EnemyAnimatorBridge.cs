using Mirror;
using Steading.Combat;
using UnityEngine;

namespace Steading.AI
{
    // Mecanim-driven enemy visuals. Replaces EnemyVisualAnimator's procedural
    // joint-blend rig with a real Animator playing imported Mixamo Mutant
    // animations from Assets/_Project/Art/Models/Characters/Enemies/.
    //
    // Lives on the same GameObject as EnemyController. Auto-finds the Animator
    // either on root or in a child (the imported Enemy_Draugr.fbx is typically
    // a child VisualRig). Implements IEnemyVisuals so EnemyController works
    // without caring which visual layer is attached.
    public class EnemyAnimatorBridge : NetworkBehaviour, IEnemyVisuals
    {
        private static readonly int HashSpeed       = Animator.StringToHash("Speed");
        private static readonly int HashAttack      = Animator.StringToHash("Attack");
        private static readonly int HashHeavyAttack = Animator.StringToHash("HeavyAttack");
        private static readonly int HashJumpAttack  = Animator.StringToHash("JumpAttack");
        private static readonly int HashHitReact    = Animator.StringToHash("HitReact");
        private static readonly int HashDie         = Animator.StringToHash("Die");

        [SerializeField] private float speedSmoothing = 12f;

        private Animator _animator;
        private UnityEngine.AI.NavMeshAgent _agent;
        private Health _health;
        private float _smoothedSpeed;
        private bool _wired;

        // ---------- IEnemyVisuals ----------

        public void EnsureRig() { /* Mecanim rig comes from imported FBX — no procedural setup needed. */ }

        public void PlayAttack(int variant)
        {
            EnsureAnimator();
            if (_animator == null) return;
            // 0 -> light, 1 -> heavy, 2 -> jump (matches the random variant
            // PerformMelee picks; if EnemyController later differentiates, the
            // mapping stays the same).
            switch (variant)
            {
                case 1: _animator.SetTrigger(HashHeavyAttack); break;
                case 2: _animator.SetTrigger(HashJumpAttack);  break;
                default: _animator.SetTrigger(HashAttack);     break;
            }
        }

        public void PlayStagger(float seconds)
        {
            EnsureAnimator();
            if (_animator == null) return;
            _animator.SetTrigger(HashHitReact);
        }

        // ---------- Lifecycle ----------

        private void Awake()
        {
            EnsureAnimator();
            _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            _health = GetComponent<Health>();
        }

        private void OnEnable() => Wire();
        private void OnDisable() => Unwire();

        private void Wire()
        {
            if (_wired || _health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died    += OnDied;
            _wired = true;
        }

        private void Unwire()
        {
            if (!_wired || _health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died    -= OnDied;
            _wired = false;
        }

        private void OnDamaged(DamageInfo info) => PlayStagger(0.4f);

        private void OnDied()
        {
            EnsureAnimator();
            if (_animator != null) _animator.SetTrigger(HashDie);
        }

        // ---------- Per-frame parameters ----------

        private void Update()
        {
            EnsureAnimator();
            if (_animator == null) return;

            float rawSpeed = _agent != null ? _agent.velocity.magnitude : 0f;
            float t = 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, t);
            _animator.SetFloat(HashSpeed, _smoothedSpeed);
        }

        // ---------- Helpers ----------

        private void EnsureAnimator()
        {
            // Unity-aware == null catches destroyed components; ?: would not.
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    _animator.applyRootMotion = false;
                    _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }
        }
    }
}
