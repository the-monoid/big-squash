using System.Collections;
using System.Collections.Generic;
using Steading.Art;
using Mirror;
using Steading.AI;
using Steading.Building;
using Steading.Player;
using Steading.World;
using UnityEngine;

namespace Steading.Combat
{
    public class PlayerAttack : NetworkBehaviour
    {
        [Header("Sword")]
        [SerializeField] private int swordLightDamage = 30;
        [SerializeField] private int swordHeavyDamage = 48;
        [SerializeField] private float swordCooldown = 0.40f;
        [SerializeField] private float swordHeavyCooldown = 0.70f;

        [Header("Axe")]
        [SerializeField] private int axeLightDamage = 22;
        [SerializeField] private int axeHeavyDamage = 38;
        [SerializeField] private int axeChopDamage = 28;
        [SerializeField] private int axeHeavyChopDamage = 48;
        [SerializeField] private float axeCooldown = 0.52f;
        [SerializeField] private float axeHeavyCooldown = 0.86f;

        [Header("Hitbox")]
        [SerializeField] private float hitDelay = 0.11f;
        [SerializeField] private float range = 2.35f;
        [SerializeField] private float slashRadius = 0.78f;
        [SerializeField] private float maxCameraOriginDistance = 5f;
        [SerializeField] private float knockbackImpulse = 4f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Defense")]
        [SerializeField] private int blockArmor = 18;
        [SerializeField] private float parryWindow = 0.25f;
        [SerializeField] private float parryStaggerSeconds = 1.4f;

        [Header("Shield Bash (legacy F-key fallback)")]
        [SerializeField] private int shieldBashDamage = 12;
        [SerializeField] private float shieldBashRange = 1.75f;
        [SerializeField] private float shieldBashRadius = 0.95f;
        [SerializeField] private float shieldBashCooldown = 0.82f;
        [SerializeField] private float shieldBashWindup = 0.10f;
        [SerializeField] private float shieldBashStaggerSeconds = 1.20f;
        [SerializeField] private float shieldBashKnockback = 13f;
        [SerializeField] private float rightMouseBlockHoldDelay = 0.24f;

        [Header("Shield Rush (New-World style — hold RMB + tap LMB)")]
        [SerializeField] private int   shieldRushDamage = 18;
        [SerializeField] private float shieldRushDistance = 4.0f;
        [SerializeField] private float shieldRushDuration = 0.30f;
        [SerializeField] private float shieldRushRadius = 0.55f;
        [SerializeField] private float shieldRushCooldown = 6.0f;
        [SerializeField] private float shieldRushKnockdownSeconds = 1.8f;
        [SerializeField] private float shieldRushPushDistance = 1.4f;

        [Header("Charged Power Bash (crouch + hold LMB)")]
        [SerializeField] private int   powerBashMinDamage = 12;
        [SerializeField] private int   powerBashMaxDamage = 24;
        [SerializeField] private float powerBashChargeMin = 0.5f;
        [SerializeField] private float powerBashChargeMax = 1.5f;
        [SerializeField] private float powerBashCooldown = 4.0f;
        [SerializeField] private float powerBashRange = 1.9f;
        [SerializeField] private float powerBashRadius = 1.05f;
        [SerializeField] private float powerBashStagger = 1.10f;
        [SerializeField] private float powerBashKnockbackMax = 16f;

        [Header("Skills")]
        [SerializeField] private float skillCooldown = 2.8f;
        [SerializeField] private float skillWindup = 0.16f;
        [SerializeField] private int swordSkillDamage = 44;
        [SerializeField] private int axeSkillDamage = 56;
        [SerializeField] private int axeSkillChopDamage = 100;
        [SerializeField] private float skillRange = 3.05f;
        [SerializeField] private float skillRadius = 1.30f;
        [SerializeField] private float skillKnockback = 7f;
        [SerializeField] private float skillStaggerSeconds = 0.55f;

        [Header("Hand Pose")]
        [SerializeField] private Vector3 handWeaponLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 swordIdleEuler = new Vector3(178f, -8f, 6f);
        [SerializeField] private Vector3 swordWindupEuler = new Vector3(148f, -24f, 28f);
        [SerializeField] private Vector3 swordSlashEuler = new Vector3(202f, 28f, -22f);
        [SerializeField] private Vector3 axeIdleEuler = new Vector3(178f, -6f, 10f);
        [SerializeField] private Vector3 axeWindupEuler = new Vector3(136f, -30f, 30f);
        [SerializeField] private Vector3 axeSlashEuler = new Vector3(214f, 24f, -24f);

        [SyncVar(hook = nameof(OnEquippedWeaponChanged))]
        private WeaponKind _equippedWeapon = WeaponKind.Sword;

        [SyncVar] private bool _blocking;

        private float _nextAttackTime;
        private float _nextBashTime;
        private float _nextRushTime;
        private float _nextPowerBashTime;
        private float _nextSkillTime;
        private float _rightMouseBlockAllowedAt;
        private float _lastBlockStartedServer = -999f;
        private int _comboStep;
        private bool _serverAttackPending;
        private bool _localBlockingSent;
        private bool _localChargingPowerBash;
        private float _powerBashChargeStart;
        private PlayerInput _input;
        private BuildController _buildController;
        private PlayerAnimatorBridge _visualAnimator;
        private Transform _swordRoot;
        private Transform _axeRoot;
        private Coroutine _swingRoutine;
        private readonly HashSet<Health> _hitBuffer = new HashSet<Health>();
        private readonly HashSet<ResourceNode> _resourceHitBuffer = new HashSet<ResourceNode>();

        public WeaponKind EquippedWeapon => _equippedWeapon;
        public bool IsBlocking => _blocking;

        private void Awake()
        {
            _buildController = GetComponent<BuildController>();
            _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            _input = GetComponent<PlayerInput>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureWeaponModels();
            RefreshEquippedWeaponVisuals();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) CmdEquipWeapon(WeaponKind.Sword);
            if (Input.GetKeyDown(KeyCode.Alpha2)) CmdEquipWeapon(WeaponKind.Axe);

            if (_buildController != null && _buildController.InBuildMode)
            {
                SendBlocking(false);
                return;
            }

            var cam = Camera.main;
            var rightMouseDown = Input.GetMouseButtonDown(1);
            if (rightMouseDown)
            {
                _rightMouseBlockAllowedAt = Time.time + rightMouseBlockHoldDelay;
            }

            if (cam == null)
            {
                SendBlocking(false);
                return;
            }

            // Legacy F-key fallback for stationary shield bash. Kept so testing
            // is unaffected. The new dash-based behavior lives in CmdStartShieldRush.
            if (Input.GetKeyDown(KeyCode.F) && Time.time >= _nextBashTime)
            {
                SendBlocking(false);
                _nextBashTime = Time.time + shieldBashCooldown;
                _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + 0.20f);
                CmdStartShieldBash(cam.transform.position, cam.transform.forward);
                return;
            }

            // RMB blocking state (with hold-delay so RMB-tap isn't a block)
            SendBlocking(Input.GetMouseButton(1) && Time.time >= _rightMouseBlockAllowedAt);

            // ---- Shield Rush: hold RMB (blocking) + tap LMB ----
            if (_localBlockingSent && Input.GetMouseButtonDown(0) && Time.time >= _nextRushTime)
            {
                SendBlocking(false);
                _nextRushTime = Time.time + shieldRushCooldown;
                _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + shieldRushDuration + 0.10f);
                CmdStartShieldRush(cam.transform.forward);
                return;
            }

            // ---- Charged Power Bash: crouch + hold LMB (charge), release to fire ----
            var crouching = _input != null && _input.CrouchHeld;
            if (crouching)
            {
                if (Input.GetMouseButtonDown(0) && Time.time >= _nextPowerBashTime && !_localBlockingSent)
                {
                    _localChargingPowerBash = true;
                    _powerBashChargeStart = Time.time;
                }
                else if (Input.GetMouseButtonUp(0) && _localChargingPowerBash)
                {
                    var charged = Time.time - _powerBashChargeStart;
                    var pct = Mathf.Clamp01((charged - powerBashChargeMin) / Mathf.Max(0.001f, powerBashChargeMax - powerBashChargeMin));
                    _localChargingPowerBash = false;
                    if (charged >= powerBashChargeMin)
                    {
                        _nextPowerBashTime = Time.time + powerBashCooldown;
                        _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + 0.30f);
                        CmdReleasePowerBash(cam.transform.forward, pct);
                    }
                }
                return;     // crouch eats LMB; don't fall into normal swing logic
            }
            else if (_localChargingPowerBash)
            {
                // Released crouch mid-charge — abort
                _localChargingPowerBash = false;
            }

            if (Input.GetKeyDown(KeyCode.Q) && Time.time >= _nextSkillTime && !_localBlockingSent)
            {
                _nextSkillTime = Time.time + skillCooldown;
                _nextAttackTime = Time.time + Mathf.Max(0.35f, GetCooldown(_equippedWeapon, heavy: true));
                CmdStartWeaponSkill(cam.transform.position, cam.transform.forward, _equippedWeapon);
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (_localBlockingSent) return;
            if (Time.time < _nextAttackTime) return;

            var heavy = Input.GetKey(KeyCode.LeftControl) || Input.GetMouseButton(2);
            _nextAttackTime = Time.time + GetCooldown(_equippedWeapon, heavy);
            _comboStep = heavy ? 2 : (_comboStep + 1) % 3;

            CmdStartWeaponAttack(cam.transform.position, cam.transform.forward, heavy, _comboStep);
        }

        private void OnGUI()
        {
            if (!isLocalPlayer) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
            };

            var bg = new Rect(Screen.width - 220f, Screen.height - 104f, 200f, 76f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(bg.x + 12f, bg.y + 8f, 180f, 24f), $"Weapon: {_equippedWeapon}", style);
            GUI.Label(new Rect(bg.x + 12f, bg.y + 31f, 180f, 24f), "1 Sword  2 Axe  Q Skill", style);
            GUI.Label(new Rect(bg.x + 12f, bg.y + 54f, 180f, 24f), _localBlockingSent ? "Holding Block" : "RMB Bash / Hold Block", style);
        }

        private void SendBlocking(bool blocking)
        {
            if (_localBlockingSent == blocking) return;
            _localBlockingSent = blocking;
            CmdSetBlocking(blocking);
        }

        [Command]
        private void CmdEquipWeapon(WeaponKind weapon)
        {
            if (weapon != WeaponKind.Sword && weapon != WeaponKind.Axe) return;
            _equippedWeapon = weapon;
        }

        [Command]
        private void CmdSetBlocking(bool blocking)
        {
            if (_blocking == blocking) return;
            _blocking = blocking;
            if (blocking) _lastBlockStartedServer = Time.time;
        }

        [Command]
        private void CmdStartWeaponAttack(Vector3 origin, Vector3 dir, bool heavy, int comboStep)
        {
            if (_serverAttackPending) return;
            if (_blocking) return;
            if (Vector3.Distance(origin, transform.position) > maxCameraOriginDistance) return;
            if (dir.sqrMagnitude < 0.001f) return;

            StartCoroutine(ServerAttackAfterWindup(origin, dir.normalized, _equippedWeapon, heavy, comboStep));
            RpcPlayWeaponSwing(_equippedWeapon, heavy, comboStep);
        }

        [Command]
        private void CmdStartShieldBash(Vector3 origin, Vector3 dir)
        {
            if (_serverAttackPending) return;
            if (Vector3.Distance(origin, transform.position) > maxCameraOriginDistance) return;
            if (dir.sqrMagnitude < 0.001f) return;

            _blocking = false;
            StartCoroutine(ServerShieldBashAfterWindup(dir.normalized));
            RpcPlayShieldBash();
        }

        // Shield Rush: forward dash, knockdown first enemy in path
        [Command]
        private void CmdStartShieldRush(Vector3 dir)
        {
            if (_serverAttackPending) return;
            if (dir.sqrMagnitude < 0.001f) return;
            var flat = new Vector3(dir.x, 0f, dir.z).normalized;

            _blocking = false;
            StartCoroutine(ServerShieldRush(flat));
            RpcPlayShieldRush(flat);
        }

        [Server]
        private IEnumerator ServerShieldRush(Vector3 dir)
        {
            _serverAttackPending = true;

            // Single sphere-cast forward — first enemy gets knocked down + damage.
            // Server is authoritative for the impact; client moves CharacterController
            // forward in the Rpc handler so the dash feels responsive.
            var origin = transform.position + Vector3.up * 0.9f;
            if (Physics.SphereCast(origin, shieldRushRadius, dir, out var hit, shieldRushDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                var ni = hit.collider.GetComponentInParent<NetworkIdentity>();
                if (ni == null || ni != netIdentity)
                {
                    var th = hit.collider.GetComponentInParent<Health>();
                    if (th != null && th.gameObject != gameObject)
                    {
                        th.TakeDamage(new DamageInfo
                        {
                            amount = shieldRushDamage,
                            hitPoint = hit.point,
                            hitDirection = dir,
                            sourceNetId = netId,
                            weaponKind = WeaponKind.Sword,    // shield, but enum doesn't have it; sword is closest
                            canBeBlocked = false,
                        });
                        var enemy = hit.collider.GetComponentInParent<EnemyController>();
                        if (enemy != null)
                        {
                            enemy.KnockdownServer(shieldRushKnockdownSeconds);
                            enemy.KnockbackServer(dir, shieldRushPushDistance * 6f);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(shieldRushDuration);
            _serverAttackPending = false;
        }

        [ClientRpc]
        private void RpcPlayShieldRush(Vector3 dir)
        {
            EnsureWeaponModels();
            if (_visualAnimator == null) _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            if (_visualAnimator != null) _visualAnimator.PlayShieldRushPose();

            // Owner-side dash motion. The CharacterController moves forward over the
            // dash duration; NetworkTransform replicates to other clients.
            if (isLocalPlayer) StartCoroutine(LocalShieldRushDash(dir));
        }

        private IEnumerator LocalShieldRushDash(Vector3 dir)
        {
            var cc = GetComponent<CharacterController>();
            if (cc == null) yield break;
            var elapsed = 0f;
            var speed = shieldRushDistance / Mathf.Max(0.05f, shieldRushDuration);
            while (elapsed < shieldRushDuration)
            {
                var dt = Time.deltaTime;
                elapsed += dt;
                cc.Move(dir * speed * dt);
                yield return null;
            }
        }

        // Charged Power Bash: crouch + held LMB charges; release fires scaled hit
        [Command]
        private void CmdReleasePowerBash(Vector3 dir, float chargePct)
        {
            if (_serverAttackPending) return;
            if (dir.sqrMagnitude < 0.001f) return;
            chargePct = Mathf.Clamp01(chargePct);

            StartCoroutine(ServerPowerBash(dir.normalized, chargePct));
            RpcPlayPowerBash(chargePct);
        }

        [Server]
        private IEnumerator ServerPowerBash(Vector3 dir, float chargePct)
        {
            _serverAttackPending = true;
            yield return new WaitForSeconds(0.10f);

            var damage = Mathf.RoundToInt(Mathf.Lerp(powerBashMinDamage, powerBashMaxDamage, chargePct));
            var knockback = Mathf.Lerp(6f, powerBashKnockbackMax, chargePct);

            var center = transform.position + Vector3.up * 1.05f + dir * (powerBashRange * 0.55f);
            var hits = Physics.OverlapSphere(center, powerBashRadius, hitLayers, QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
            {
                var ni = col.GetComponentInParent<NetworkIdentity>();
                if (ni != null && ni == netIdentity) continue;
                var th = col.GetComponentInParent<Health>();
                if (th == null || th.gameObject == gameObject) continue;

                th.TakeDamage(new DamageInfo
                {
                    amount = damage,
                    hitPoint = col.bounds.center,
                    hitDirection = dir,
                    sourceNetId = netId,
                    weaponKind = WeaponKind.Sword,
                    canBeBlocked = false,
                });
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    enemy.StaggerServer(powerBashStagger);
                    enemy.KnockbackServer(dir, knockback);
                }
                break;  // one target per bash
            }

            yield return new WaitForSeconds(0.30f);
            _serverAttackPending = false;
        }

        [ClientRpc]
        private void RpcPlayPowerBash(float chargePct)
        {
            EnsureWeaponModels();
            if (_visualAnimator == null) _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            if (_visualAnimator != null) _visualAnimator.PlayPowerBashPose();
        }

        [Command]
        private void CmdStartWeaponSkill(Vector3 origin, Vector3 dir, WeaponKind weapon)
        {
            if (_serverAttackPending) return;
            if (_blocking) return;
            if (weapon != WeaponKind.Sword && weapon != WeaponKind.Axe) return;
            if (Vector3.Distance(origin, transform.position) > maxCameraOriginDistance) return;
            if (dir.sqrMagnitude < 0.001f) return;

            var activeWeapon = _equippedWeapon;
            StartCoroutine(ServerWeaponSkillAfterWindup(dir.normalized, activeWeapon));
            RpcPlayWeaponSkill(activeWeapon);
        }

        [Server]
        private IEnumerator ServerAttackAfterWindup(Vector3 origin, Vector3 dir, WeaponKind weapon, bool heavy, int comboStep)
        {
            _serverAttackPending = true;
            yield return new WaitForSeconds(hitDelay);
            ApplyWeaponHit(origin, dir, weapon, heavy, comboStep);
            yield return new WaitForSeconds(Mathf.Max(0.05f, GetCooldown(weapon, heavy) - hitDelay));
            _serverAttackPending = false;
        }

        [Server]
        private IEnumerator ServerShieldBashAfterWindup(Vector3 dir)
        {
            _serverAttackPending = true;
            yield return new WaitForSeconds(shieldBashWindup);
            ApplyShieldBash(dir);
            yield return new WaitForSeconds(0.22f);
            _serverAttackPending = false;
        }

        [Server]
        private IEnumerator ServerWeaponSkillAfterWindup(Vector3 dir, WeaponKind weapon)
        {
            _serverAttackPending = true;
            yield return new WaitForSeconds(skillWindup);
            ApplyWeaponSkill(dir, weapon);
            yield return new WaitForSeconds(0.30f);
            _serverAttackPending = false;
        }

        [Server]
        private void ApplyWeaponHit(Vector3 origin, Vector3 dir, WeaponKind weapon, bool heavy, int comboStep)
        {
            _hitBuffer.Clear();
            _resourceHitBuffer.Clear();

            var playerCenter = transform.position + Vector3.up * 1.05f;
            var slashCenter = playerCenter + dir * (range * 0.62f);
            var hits = Physics.OverlapSphere(slashCenter, slashRadius, hitLayers, QueryTriggerInteraction.Ignore);

            foreach (var col in hits)
            {
                TryHitHealth(col, playerCenter, dir, weapon, heavy, comboStep);
                TryHitResource(col, weapon, heavy);
            }

            var rayHits = Physics.RaycastAll(origin, dir, maxCameraOriginDistance + range, hitLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(rayHits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in rayHits)
            {
                var ni = hit.collider.GetComponentInParent<NetworkIdentity>();
                if (ni != null && ni == netIdentity) continue;
                TryHitResource(hit.collider, weapon, heavy);
                if (_resourceHitBuffer.Count > 0) break;
            }
        }

        [Server]
        private void ApplyShieldBash(Vector3 dir)
        {
            _hitBuffer.Clear();

            var playerCenter = transform.position + Vector3.up * 1.05f;
            var bashCenter = playerCenter + dir * (shieldBashRange * 0.55f);
            var hits = Physics.OverlapSphere(bashCenter, shieldBashRadius, hitLayers, QueryTriggerInteraction.Ignore);

            foreach (var col in hits)
            {
                TryHitHealth(col, playerCenter, dir, _equippedWeapon, heavy: false, comboStep: 0, damageOverride: shieldBashDamage, canBeBlocked: false, staggerSeconds: shieldBashStaggerSeconds, knockbackOverride: shieldBashKnockback, rangeOverride: shieldBashRange);
            }
        }

        [Server]
        private void ApplyWeaponSkill(Vector3 dir, WeaponKind weapon)
        {
            _hitBuffer.Clear();
            _resourceHitBuffer.Clear();

            var playerCenter = transform.position + Vector3.up * 1.05f;
            var skillCenter = playerCenter + dir * (skillRange * 0.48f);
            var hits = Physics.OverlapSphere(skillCenter, skillRadius, hitLayers, QueryTriggerInteraction.Ignore);
            var damage = weapon == WeaponKind.Axe ? axeSkillDamage : swordSkillDamage;

            foreach (var col in hits)
            {
                TryHitHealth(col, playerCenter, dir, weapon, heavy: true, comboStep: 2, damageOverride: damage, canBeBlocked: true, staggerSeconds: skillStaggerSeconds, knockbackOverride: skillKnockback, rangeOverride: skillRange);
                if (weapon == WeaponKind.Axe)
                {
                    TryHitResource(col, weapon, heavy: true, axeSkillChopDamage);
                }
            }
        }

        [Server]
        private void TryHitHealth(Collider col, Vector3 playerCenter, Vector3 dir, WeaponKind weapon, bool heavy, int comboStep, int damageOverride = -1, bool canBeBlocked = true, float staggerSeconds = 0f, float knockbackOverride = -1f, float rangeOverride = -1f)
        {
            var target = col.GetComponentInParent<Health>();
            if (target == null || target.gameObject == gameObject || _hitBuffer.Contains(target)) return;

            var toTarget = target.transform.position - playerCenter;
            var flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            var effectiveRange = rangeOverride > 0f ? rangeOverride : range;
            if (flatToTarget.sqrMagnitude > effectiveRange * effectiveRange) return;
            if (Vector3.Dot(dir, flatToTarget.normalized) < -0.15f) return;

            _hitBuffer.Add(target);
            var damage = damageOverride >= 0 ? damageOverride : GetDamage(weapon, heavy, comboStep);
            target.TakeDamage(new DamageInfo
            {
                amount = damage,
                hitPoint = target.transform.position + Vector3.up,
                hitDirection = dir,
                sourceNetId = netId,
                weaponKind = weapon,
                canBeBlocked = canBeBlocked,
            });

            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce((dir + Vector3.up * 0.15f) * (knockbackOverride > 0f ? knockbackOverride : knockbackImpulse), ForceMode.Impulse);
            }

            if (staggerSeconds > 0f)
            {
                var enemy = target.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.StaggerServer(staggerSeconds);
                    enemy.KnockbackServer(dir, knockbackOverride > 0f ? knockbackOverride : knockbackImpulse);
                }
            }
        }

        [Server]
        private void TryHitResource(Collider col, WeaponKind weapon, bool heavy, int chopDamageOverride = -1)
        {
            if (weapon != WeaponKind.Axe) return;

            var node = col.GetComponentInParent<ResourceNode>();
            if (node == null || node.IsDepleted || _resourceHitBuffer.Contains(node)) return;
            _resourceHitBuffer.Add(node);

            var depleted = node.ApplyToolHit(weapon, chopDamageOverride >= 0 ? chopDamageOverride : GetChopDamage(weapon, heavy), netIdentity, out _);
            RpcApplyResourceHit(node.NodeId, depleted);
        }

        public bool TryMitigateIncomingDamage(ref DamageInfo info)
        {
            if (!_blocking || !info.canBeBlocked || info.amount <= 0) return false;

            var incoming = info.hitDirection.sqrMagnitude > 0.001f
                ? -info.hitDirection.normalized
                : transform.forward;
            if (Vector3.Dot(transform.forward, incoming) < 0.15f) return false;

            var parried = Time.time - _lastBlockStartedServer <= parryWindow;
            if (parried)
            {
                info.amount = 0;
                StaggerSource(info.sourceNetId);
                RpcShowDefensePulse(true);
                return true;
            }

            var armor = _equippedWeapon == WeaponKind.Axe ? Mathf.RoundToInt(blockArmor * 0.85f) : blockArmor;
            info.amount = Mathf.Max(0, info.amount - armor);
            RpcShowDefensePulse(false);
            return true;
        }

        [Server]
        private void StaggerSource(uint sourceNetId)
        {
            if (sourceNetId == 0) return;
            if (!NetworkServer.spawned.TryGetValue(sourceNetId, out var source)) return;
            var enemy = source.GetComponent<EnemyController>();
            if (enemy != null) enemy.StaggerServer(parryStaggerSeconds);
        }

        [ClientRpc]
        private void RpcPlayWeaponSwing(WeaponKind weapon, bool heavy, int comboStep)
        {
            EnsureWeaponModels();
            if (_visualAnimator == null) _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            if (_visualAnimator != null) _visualAnimator.PlaySwordAttackPose(heavy, comboStep);

            if (_swingRoutine != null) StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(AnimateWeaponSwing(weapon, heavy, comboStep));
        }

        [ClientRpc]
        private void RpcPlayShieldBash()
        {
            EnsureWeaponModels();
            if (_visualAnimator == null) _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            if (_visualAnimator != null) _visualAnimator.PlayShieldBashPose();
            StartCoroutine(PulseWeapon(GetActiveWeaponRoot(), 1.08f, 0.045f));
        }

        [ClientRpc]
        private void RpcPlayWeaponSkill(WeaponKind weapon)
        {
            EnsureWeaponModels();
            if (_visualAnimator == null) _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            if (_visualAnimator != null) _visualAnimator.PlaySkillAttackPose(weapon == WeaponKind.Axe);

            if (_swingRoutine != null) StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(AnimateWeaponSkill(weapon));
        }

        [ClientRpc]
        private void RpcApplyResourceHit(string nodeId, bool depleted)
        {
            if (ResourceNode.TryFind(nodeId, out var node))
            {
                node.ApplyClientHit(depleted);
            }
        }

        [ClientRpc]
        private void RpcShowDefensePulse(bool parried)
        {
            EnsureWeaponModels();
            var root = GetActiveWeaponRoot();
            if (root == null) return;
            StartCoroutine(PulseWeapon(root, parried ? 1.20f : 1.08f, parried ? 0.09f : 0.045f));
        }

        private void OnEquippedWeaponChanged(WeaponKind oldWeapon, WeaponKind newWeapon)
        {
            RefreshEquippedWeaponVisuals();
        }

        private void EnsureWeaponModels()
        {
            if (_swordRoot == null) CreateSwordModel();
            if (_axeRoot == null) CreateAxeModel();
            AttachWeaponsToHand();
        }

        private void AttachWeaponsToHand()
        {
            var parent = GetWeaponParent();
            AttachWeapon(_swordRoot, parent, swordIdleEuler);
            AttachWeapon(_axeRoot, parent, axeIdleEuler);
            RefreshEquippedWeaponVisuals();
        }

        private void AttachWeapon(Transform weapon, Transform parent, Vector3 idleEuler)
        {
            if (weapon == null) return;
            weapon.SetParent(parent, false);
            weapon.localPosition = handWeaponLocalPosition;
            weapon.localRotation = Quaternion.Euler(idleEuler);
        }

        private Transform GetWeaponParent()
        {
            if (_visualAnimator == null) _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            if (_visualAnimator == null) _visualAnimator = gameObject.AddComponent<PlayerAnimatorBridge>();
            _visualAnimator.EnsureRig();
            return _visualAnimator.RightHandSocket != null ? _visualAnimator.RightHandSocket : transform;
        }

        private void RefreshEquippedWeaponVisuals()
        {
            if (_swordRoot != null) _swordRoot.gameObject.SetActive(_equippedWeapon == WeaponKind.Sword);
            if (_axeRoot != null) _axeRoot.gameObject.SetActive(_equippedWeapon == WeaponKind.Axe);
        }

        private Transform GetActiveWeaponRoot()
        {
            return _equippedWeapon == WeaponKind.Axe ? _axeRoot : _swordRoot;
        }

        private IEnumerator AnimateWeaponSwing(WeaponKind weapon, bool heavy, int comboStep)
        {
            var root = weapon == WeaponKind.Axe ? _axeRoot : _swordRoot;
            if (root == null) yield break;

            var idle = Quaternion.Euler(weapon == WeaponKind.Axe ? axeIdleEuler : swordIdleEuler);
            var windup = Quaternion.Euler(weapon == WeaponKind.Axe ? axeWindupEuler : swordWindupEuler);
            var slash = Quaternion.Euler(weapon == WeaponKind.Axe ? axeSlashEuler : swordSlashEuler);
            if (!heavy && comboStep == 1) slash *= Quaternion.Euler(0f, 0f, 18f);
            if (!heavy && comboStep == 2) slash *= Quaternion.Euler(0f, 0f, -22f);

            yield return RotateWeapon(root, idle, windup, heavy ? 0.18f : 0.10f);
            yield return RotateWeapon(root, windup, slash, heavy ? 0.13f : 0.09f);
            yield return PulseWeapon(root, heavy ? 1.12f : 1.06f, 0.035f);
            yield return RotateWeapon(root, slash, idle, heavy ? 0.28f : 0.18f);
        }

        private IEnumerator AnimateWeaponSkill(WeaponKind weapon)
        {
            var root = weapon == WeaponKind.Axe ? _axeRoot : _swordRoot;
            if (root == null) yield break;

            var idle = Quaternion.Euler(weapon == WeaponKind.Axe ? axeIdleEuler : swordIdleEuler);
            var windup = Quaternion.Euler(weapon == WeaponKind.Axe ? axeWindupEuler + new Vector3(-18f, -10f, 16f) : swordWindupEuler + new Vector3(-12f, -8f, 12f));
            var slash = Quaternion.Euler(weapon == WeaponKind.Axe ? axeSlashEuler + new Vector3(18f, 12f, -18f) : swordSlashEuler + new Vector3(14f, 14f, -16f));

            yield return RotateWeapon(root, idle, windup, 0.14f);
            yield return RotateWeapon(root, windup, slash, 0.13f);
            yield return PulseWeapon(root, 1.18f, 0.05f);
            yield return RotateWeapon(root, slash, idle, 0.26f);
        }

        private IEnumerator RotateWeapon(Transform root, Quaternion from, Quaternion to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                root.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            if (root != null) root.localRotation = to;
        }

        private IEnumerator PulseWeapon(Transform root, float scale, float duration)
        {
            if (root == null) yield break;
            var original = root.localScale;
            root.localScale = original * scale;
            yield return new WaitForSeconds(duration);
            if (root != null) root.localScale = original;
        }

        private int GetDamage(WeaponKind weapon, bool heavy, int comboStep)
        {
            var baseDamage = weapon == WeaponKind.Axe
                ? (heavy ? axeHeavyDamage : axeLightDamage)
                : (heavy ? swordHeavyDamage : swordLightDamage);
            return !heavy && comboStep == 2 ? Mathf.RoundToInt(baseDamage * 1.35f) : baseDamage;
        }

        private int GetChopDamage(WeaponKind weapon, bool heavy)
        {
            if (weapon != WeaponKind.Axe) return 1;
            return heavy ? axeHeavyChopDamage : axeChopDamage;
        }

        private float GetCooldown(WeaponKind weapon, bool heavy)
        {
            if (weapon == WeaponKind.Axe) return heavy ? axeHeavyCooldown : axeCooldown;
            return heavy ? swordHeavyCooldown : swordCooldown;
        }

        private void CreateSwordModel()
        {
            var bladeMat = CreateRuntimeMaterial("SwordBlade", new Color(0.72f, 0.74f, 0.76f), 0.52f, 0.30f);
            var guardMat = CreateRuntimeMaterial("SwordGuard", new Color(0.62f, 0.48f, 0.26f), 0.35f, 0.15f);
            var gripMat = CreateRuntimeMaterial("SwordGrip", new Color(0.14f, 0.09f, 0.06f), 0.22f, 0f);

            _swordRoot = new GameObject("SwordRoot").transform;
            CreateTube(_swordRoot, "Grip", new Vector3(0f, 0f, 0f), 0.36f, 0.047f, 0.040f, gripMat, Vector3.zero);
            CreateEllipsoid(_swordRoot, "Pommel", new Vector3(0f, -0.245f, 0f), new Vector3(0.095f, 0.060f, 0.095f), guardMat);
            CreateTube(_swordRoot, "Guard", new Vector3(0f, 0.215f, 0f), 0.54f, 0.040f, 0.030f, guardMat, new Vector3(0f, 0f, 90f));
            CreateBlade(_swordRoot, "Blade", new Vector3(0f, 0.255f, 0f), 1.22f, 0.078f, 0.045f, bladeMat);
            CreateTube(_swordRoot, "Fuller", new Vector3(0f, 0.820f, 0.027f), 0.82f, 0.010f, 0.004f, guardMat, Vector3.zero);
        }

        private void CreateAxeModel()
        {
            var metal = CreateRuntimeMaterial("AxeIron", new Color(0.56f, 0.58f, 0.56f), 0.42f, 0.28f);
            var edge = CreateRuntimeMaterial("AxeEdge", new Color(0.76f, 0.77f, 0.73f), 0.55f, 0.35f);
            var wood = CreateRuntimeMaterial("AxeHandle", new Color(0.30f, 0.18f, 0.09f), 0.30f, 0f);
            var leather = CreateRuntimeMaterial("AxeWrap", new Color(0.12f, 0.07f, 0.04f), 0.24f, 0f);

            _axeRoot = new GameObject("AxeRoot").transform;
            CreateTube(_axeRoot, "Handle", new Vector3(0f, 0.16f, 0f), 0.92f, 0.038f, 0.032f, wood, Vector3.zero);
            CreateTube(_axeRoot, "GripWrap", new Vector3(0f, -0.16f, 0f), 0.26f, 0.043f, 0.037f, leather, Vector3.zero);
            CreateTube(_axeRoot, "AxeEye", new Vector3(0f, 0.56f, 0f), 0.22f, 0.062f, 0.050f, metal, new Vector3(0f, 0f, 90f));
            CreateAxeHead(_axeRoot, "AxeHead", new Vector3(0.11f, 0.58f, 0f), 0.36f, 0.33f, 0.050f, metal, edge);
        }

        private static void CreateTube(Transform parent, string name, Vector3 localPosition, float length, float radiusX, float radiusZ, Material mat, Vector3 localEuler)
        {
            CreateMesh(parent, name, localPosition, Quaternion.Euler(localEuler), BuildTubeMesh(length, radiusX, radiusZ, 28), mat);
        }

        private static void CreateEllipsoid(Transform parent, string name, Vector3 localPosition, Vector3 radius, Material mat)
        {
            CreateMesh(parent, name, localPosition, Quaternion.identity, BuildEllipsoidMesh(radius, 14, 28), mat);
        }

        private static void CreateBlade(Transform parent, string name, Vector3 localPosition, float length, float width, float depth, Material mat)
        {
            CreateMesh(parent, name, localPosition, Quaternion.identity, BuildBladeMesh(length, width, depth), mat);
        }

        private static void CreateAxeHead(Transform parent, string name, Vector3 localPosition, float height, float width, float depth, Material metal, Material edge)
        {
            CreateMesh(parent, name + "Body", localPosition, Quaternion.identity, BuildAxeHeadMesh(height, width, depth, false), metal);
            CreateMesh(parent, name + "Edge", localPosition + new Vector3(width * 0.42f, 0f, 0f), Quaternion.identity, BuildAxeHeadMesh(height * 0.92f, width * 0.25f, depth * 1.08f, true), edge);
        }

        private static void CreateMesh(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Mesh mesh, Material mat)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static Mesh BuildBladeMesh(float length, float width, float depth)
        {
            var shoulder = length * 0.88f;
            var vertices = new List<Vector3>
            {
                new Vector3(-width, 0f, 0f), new Vector3(0f, 0f, depth), new Vector3(width, 0f, 0f), new Vector3(0f, 0f, -depth),
                new Vector3(-width * 0.52f, shoulder, 0f), new Vector3(0f, shoulder, depth * 0.70f), new Vector3(width * 0.52f, shoulder, 0f), new Vector3(0f, shoulder, -depth * 0.70f),
                new Vector3(0f, length, 0f),
            };
            var triangles = new List<int>
            {
                0,4,1, 1,4,5, 1,5,2, 2,5,6, 2,6,3, 3,6,7, 3,7,0, 0,7,4,
                4,8,5, 5,8,6, 6,8,7, 7,8,4, 0,1,2, 0,2,3,
            };
            return FinalizeMesh("BladeMesh", vertices, triangles);
        }

        private static Mesh BuildAxeHeadMesh(float height, float width, float depth, bool edgeOnly)
        {
            var x0 = edgeOnly ? -width * 0.45f : -width * 0.52f;
            var x1 = width * 0.52f;
            var y0 = -height * 0.50f;
            var y1 = height * 0.50f;
            var waist = height * 0.16f;
            var vertices = new List<Vector3>
            {
                new Vector3(x0, y0 + waist, -depth), new Vector3(x1, y0, -depth), new Vector3(x1, y1, -depth), new Vector3(x0, y1 - waist, -depth),
                new Vector3(x0, y0 + waist, depth), new Vector3(x1, y0, depth), new Vector3(x1, y1, depth), new Vector3(x0, y1 - waist, depth),
            };
            var triangles = new List<int>
            {
                0,1,2, 0,2,3, 4,6,5, 4,7,6,
                0,4,1, 1,4,5, 1,5,2, 2,5,6, 2,6,3, 3,6,7, 3,7,0, 0,7,4,
            };
            return FinalizeMesh("AxeHeadMesh", vertices, triangles);
        }

        private static Mesh BuildTubeMesh(float length, float radiusX, float radiusZ, int segments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var half = length * 0.5f;

            for (int ring = 0; ring < 2; ring++)
            {
                var y = ring == 0 ? -half : half;
                for (int i = 0; i < segments; i++)
                {
                    var a = i / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radiusX, y, Mathf.Sin(a) * radiusZ));
                }
            }

            for (int i = 0; i < segments; i++)
            {
                var ni = (i + 1) % segments;
                triangles.Add(i); triangles.Add(segments + i); triangles.Add(ni);
                triangles.Add(ni); triangles.Add(segments + i); triangles.Add(segments + ni);
            }

            var bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -half, 0f));
            var topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, half, 0f));
            for (int i = 0; i < segments; i++)
            {
                var ni = (i + 1) % segments;
                triangles.Add(bottomCenter); triangles.Add(ni); triangles.Add(i);
                triangles.Add(topCenter); triangles.Add(segments + i); triangles.Add(segments + ni);
            }
            return FinalizeMesh("TubeMesh", vertices, triangles);
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
                    vertices.Add(new Vector3(Mathf.Cos(theta) * sinPhi * radius.x, cosPhi * radius.y, Mathf.Sin(theta) * sinPhi * radius.z));
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
            return FinalizeMesh("EllipsoidMesh", vertices, triangles);
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

        private static Material CreateRuntimeMaterial(string name, Color color, float smoothness, float metallic)
        {
            return ProceduralArt.CreateLitMaterial(name, color, SurfaceForMaterial(name), smoothness, metallic);
        }

        private static ArtSurface SurfaceForMaterial(string name)
        {
            if (name.Contains("Blade") || name.Contains("Iron") || name.Contains("Edge") || name.Contains("Guard")) return ArtSurface.Metal;
            if (name.Contains("Grip") || name.Contains("Wrap")) return ArtSurface.Leather;
            if (name.Contains("Handle")) return ArtSurface.Wood;
            return ArtSurface.Plain;
        }
    }
}
