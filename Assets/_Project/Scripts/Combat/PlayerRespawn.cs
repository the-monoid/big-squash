using Mirror;
using UnityEngine;

namespace Steading.Combat
{
    [RequireComponent(typeof(Health))]
    public class PlayerRespawn : NetworkBehaviour
    {
        [Tooltip("Wait this long after death (in addition to the death-anim length) before respawning.")]
        [SerializeField] private float respawnDelayAfterAnim = 0.5f;
        [Tooltip("Used as the death animation duration when no Death state is found on the Animator.")]
        [SerializeField] private float fallbackDeathAnimSeconds = 2.5f;

        private Health _health;
        private Animator _animator;
        private float _respawnAt;
        private bool _waitingToRespawn;

        private void Awake() => _health = GetComponent<Health>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        [ServerCallback]
        private void Update()
        {
            if (!_waitingToRespawn || Time.time < _respawnAt) return;
            _waitingToRespawn = false;
            DoRespawn();
        }

        [Server]
        private void OnDied()
        {
            // Wait the full death-anim duration + a beat before respawning, so
            // the body completes its collapse before snapping back to spawn.
            // Animator lives on the visual child for the imported character.
            _animator = _animator != null ? _animator : GetComponentInChildren<Animator>();
            float deathAnimLen = ResolveDeathAnimLength();
            _waitingToRespawn = true;
            _respawnAt = Time.time + deathAnimLen + respawnDelayAfterAnim;
        }

        // Best-effort: read the actual clip length of the "Death" state from
        // the Animator's controller. Falls back to fallbackDeathAnimSeconds
        // if no Death state is found (shouldn't happen for a built controller).
        private float ResolveDeathAnimLength()
        {
            if (_animator == null) return fallbackDeathAnimSeconds;
            var controller = _animator.runtimeAnimatorController;
            if (controller == null) return fallbackDeathAnimSeconds;
            foreach (var clip in controller.animationClips)
            {
                if (clip == null) continue;
                var name = clip.name.ToLowerInvariant();
                if (name.Contains("death") || name.Contains("dying"))
                {
                    return Mathf.Max(0.6f, clip.length);
                }
            }
            return fallbackDeathAnimSeconds;
        }

        [Server]
        private void DoRespawn()
        {
            var spawn = NetworkManager.startPositions.Count > 0
                ? NetworkManager.startPositions[Random.Range(0, NetworkManager.startPositions.Count)]
                : null;

            var pos = spawn != null ? spawn.position : Vector3.zero;
            var rot = spawn != null ? spawn.rotation : Quaternion.identity;

            // CharacterController must be disabled when teleporting or it overrides the move.
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.SetPositionAndRotation(pos, rot);
            RpcSnapToTransform(pos, rot);

            if (cc != null) cc.enabled = true;

            _health.ResetToFull();

            // Reset the Animator's death/hit triggers so the rig actually
            // gets back up. Otherwise the next death wouldn't re-fire the
            // trigger and the body stays prone.
            if (_animator != null)
            {
                _animator.ResetTrigger("Die");
                _animator.ResetTrigger("HitReact");
                // Force re-enter Locomotion via a 0-duration crossfade.
                _animator.CrossFade("Locomotion", 0.05f, 0);
            }
        }

        [ClientRpc]
        private void RpcSnapToTransform(Vector3 pos, Quaternion rot)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            if (cc != null) cc.enabled = true;

            // Mirror the trigger-reset on every client so the death pose
            // doesn't linger after the teleport.
            var anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.ResetTrigger("Die");
                anim.ResetTrigger("HitReact");
                anim.CrossFade("Locomotion", 0.05f, 0);
            }
        }
    }
}
