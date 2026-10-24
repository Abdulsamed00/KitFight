using System.Collections.Generic;
using UnityEngine;

namespace KitFight
{
    /// <summary>
    /// Combo, block, hit and death rules driven by Attack()/SetBlock() commands. Contains no input code.
    /// An attack always plays to its end; a click during it is queued and starts the next one afterwards.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerCombat : MonoBehaviour
    {
        public static readonly List<PlayerCombat> All = new List<PlayerCombat>();

        public float blendTime = 0.1f;
        [Tooltip("Total width of the guard cone in front of the player.")]
        public float blockAngle = 140f;
        public float stunTime = 0.4f;
        public float heavyStunTime = 0.5f;
        [Tooltip("Hits at or above this damage use the heavy hit reaction.")]
        public float heavyHitDamage = 14f;

        [Header("Knockdown (combo finisher of any kit except light ones)")]
        public float knockdownMinDamage = 12f;
        public float knockdownLieTime = 1.15f;  // on the ground (clip sped up in the controller)
        public float getUpTime = 1.1f;         // getting up; invulnerable for the whole knockdown

        [Header("Guard break")]
        public float guardBreakDamage = 45f;   // blocked damage needed to break the guard
        public float guardRecovery = 15f;      // guard damage recovered per second when not blocking
        public float guardBreakStun = 0.9f;

        [Header("Dodge")]
        public float dodgeDuration = 0.45f;     // control is locked this long (clip is sped up and blended out)
        public float dodgeInvulnerable = 0.3f;  // i-frames from the start of the dodge
        public float dodgeSpeed = 14f;          // initial dash speed (decays in PlayerMotor)
        public float dodgeDecay = 40f;          // dash slows by this much per second (~2.4 m dash in ~0.35 s)
        public float dodgeCooldown = 0.5f;      // after the dodge ends

        [Header("Stamina / special attack")]
        public float maxStamina = 100f;
        public float staminaRegen = 14f;        // per second, paused while the special runs
        public float specialCost = 60f;
        public float specialChargeTime = 0.9f;  // wind-up; a clean hit in this window cancels it (stamina is lost)
        public float specialHitTime = 0.4f;     // after the charge, when the blow lands (matched to the SpecialAttack state speed)
        public float specialRecoverTime = 0.85f;// after the charge, until control returns
        public float specialBaseDamage = 18f;   // + kit damage
        public float specialRangeBonus = 0.8f;  // + kit range
        public float specialArc = 170f;
        public float specialKnockback = 7f;

        public bool IsAttacking => step >= 0;
        public bool IsCharging => special == 1;
        public bool IsSpecial => special > 0;
        public float Stamina => stamina;
        public float StaminaFraction => Mathf.Clamp01(stamina / maxStamina);
        public bool CanSpecial => stamina >= specialCost;
        public bool IsBlocking => blocking;
        public bool IsDead => dead;
        public bool IsDodging => dodgeTimer > 0f;
        public bool IsDown => downTimer > 0f;
        public float GuardFraction => Mathf.Clamp01(guardDamage / guardBreakDamage);
        public KitData Kit => kit;
        public Health Health { get; private set; }

        KitData kit;
        Animator anim;
        PlayerMotor motor;
        int step = -1;
        float time;
        bool queued, hitDone;
        bool blockHeld, blocking;
        bool dead;
        float stunTimer, blockReactTimer;
        float dodgeTimer, dodgeCooldownTimer;
        float hitStopTimer;
        float downTimer, guardDamage;
        bool gettingUp, celebrating;
        bool baseInAttack;      // attack also playing full-body on the base layer (standing still)
        float upperWeight;
        bool hasUpperLayer;
        PlayerTint tint;
        float stamina;
        int special;            // 0 none, 1 charging, 2 striking
        float specialTimer;
        bool specialHitDone;

        static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            motor = GetComponent<PlayerMotor>();
            Health = GetComponent<Health>();
            tint = GetComponent<PlayerTint>();
            stamina = maxStamina;
        }

        void Start() => hasUpperLayer = anim && anim.layerCount > 1;

        /// <summary>Drop the upper-body attack layer immediately (knockdown, death, reset...).</summary>
        void ClearUpper()
        {
            upperWeight = 0f;
            baseInAttack = false;
            hasUpperLayer = anim && anim.runtimeAnimatorController && anim.layerCount > 1;
            if (!hasUpperLayer) return;
            anim.SetLayerWeight(1, 0f);
            anim.CrossFadeInFixedTime("Empty", 0f, 1);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetKit(KitData k)
        {
            ClearActions();
            kit = k;
        }

        /// <summary>Full reset for a new round: health, state, animation.</summary>
        public void ResetFighter()
        {
            ClearActions();
            dead = false;
            stunTimer = blockReactTimer = 0f;
            dodgeTimer = dodgeCooldownTimer = 0f;
            hitStopTimer = 0f;
            downTimer = guardDamage = 0f;
            gettingUp = celebrating = false;
            stamina = maxStamina;
            if (anim) anim.speed = 1f;
            blockHeld = false;
            Health.ResetHealth();
            if (motor) { motor.locked = false; motor.ClearImpulse(); }
        }

        void ClearActions()
        {
            step = -1;
            queued = hitDone = false;
            EndSpecial();
            ClearUpper();
            blocking = false;
            if (motor) { motor.attacking = false; motor.blocking = false; }
            if (anim && anim.runtimeAnimatorController) anim.CrossFadeInFixedTime("Locomotion", 0f);
        }

        // ---------------------------------------------------------------- commands

        bool Incapacitated => dead || celebrating || stunTimer > 0f || downTimer > 0f;

        public void Attack()
        {
            if (Incapacitated || blocking || special > 0) return;
            if (kit == null || kit.comboClips == null || kit.comboClips.Length == 0) return;
            if (dodgeTimer > 0f)
            {
                // Dash attack (light kits): cancel the dash into the first swing, keeping the momentum.
                if (!kit.dashAttack || dodgeTimer > dodgeDuration - 0.06f) return;
                dodgeTimer = 0f;
                dodgeCooldownTimer = dodgeCooldown;
                motor.locked = false;
                Begin(0);
                return;
            }
            if (step < 0) Begin(0);
            else queued = true;
        }

        /// <summary>Charged special: costs stamina, winds up for specialChargeTime, then a wide knockdown blow.</summary>
        public void SpecialAttack()
        {
            if (Incapacitated || blocking || special > 0 || step >= 0 || dodgeTimer > 0f || kit == null) return;
            if (stamina < specialCost) return;
            stamina -= specialCost;
            special = 1;
            specialTimer = 0f;
            specialHitDone = false;
            motor.FaceOpponent();
            motor.locked = true;
            if (anim) anim.CrossFadeInFixedTime("SpecialCharge", 0.1f);
        }

        public void SetBlock(bool held)
        {
            blockHeld = held;
            if (held) TryStartBlock();
        }

        /// <summary>Dash + i-frames. moveInput is the stick/key vector; zero = step back from the opponent.</summary>
        public void Dodge(Vector2 moveInput)
        {
            if (Incapacitated || step >= 0 || special > 0 || dodgeTimer > 0f || dodgeCooldownTimer > 0f) return;

            var dir = motor.WorldDirection(moveInput);
            if (dir == Vector3.zero) dir = -motor.DirectionToOpponent();
            if (dir == Vector3.zero) dir = -transform.forward;

            StopBlock(false);
            motor.FaceOpponent();   // directional clips are played relative to the opponent
            motor.locked = true;
            motor.AddImpulse(dir * dodgeSpeed, dodgeDecay);
            dodgeTimer = dodgeDuration;

            // Pick the clip by dodge direction relative to facing: back / right / left (mirrored) / forward.
            var local = transform.InverseTransformDirection(dir);
            string state = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? (local.x > 0f ? "DodgeRight" : "DodgeLeft")
                         : local.z < 0f ? "DodgeBack" : "Dodge";
            if (anim) anim.CrossFadeInFixedTime(state, 0.05f);
        }

        // ---------------------------------------------------------------- block

        void TryStartBlock()
        {
            if (Incapacitated || blocking || step >= 0 || special > 0 || kit == null || kit.blockClip == null) return;
            blocking = true;
            if (motor) motor.blocking = true;
            if (anim) anim.CrossFadeInFixedTime("Block", blendTime);
        }

        void StopBlock(bool playLocomotion)
        {
            if (!blocking) return;
            blocking = false;
            if (motor) motor.blocking = false;
            if (playLocomotion && anim) anim.CrossFadeInFixedTime("Locomotion", blendTime);
        }

        // ---------------------------------------------------------------- attack

        void Begin(int i)
        {
            step = i;
            time = 0f;
            queued = hitDone = false;
            if (motor)
            {
                motor.attacking = true;
                motor.FaceOpponent();
                // Step into the swing for weight (heavier kits lunge further), but never shove through the opponent.
                if (motor.DistanceToOpponent() > 1.2f)
                    motor.AddImpulse(motor.DirectionToOpponent() * Mathf.Lerp(3f, 5.5f, Mathf.InverseLerp(7f, 24f, kit.damage)));
            }
            if (!anim) return;
            anim.SetFloat(AttackSpeedHash, kit.attackSpeed);
            hasUpperLayer = anim.layerCount > 1;
            if (hasUpperLayer)
            {
                anim.CrossFadeInFixedTime("Attack" + i, blendTime, 1, 0f);
                upperWeight = 1f;
                anim.SetLayerWeight(1, 1f);
            }
            // Full-body when standing still; when moving the legs keep the locomotion blend.
            baseInAttack = !hasUpperLayer || !motor.HasMoveInput;
            if (baseInAttack) anim.CrossFadeInFixedTime("Attack" + i, blendTime, 0, 0f);
        }

        void DoHit()
        {
            bool finisher = step == kit.comboClips.Length - 1 && kit.damage >= knockdownMinDamage;
            HitScan(kit.damage, kit.range, kit.arc, kit.knockback, finisher, false);
        }

        /// <summary>Cone hit scan in front of the player: fighters take the hit, props shatter.</summary>
        void HitScan(float damage, float range, float arc, float knockback, bool knockdown, bool guardCrush)
        {
            foreach (var other in All)
            {
                if (other == this || other.dead) continue;
                var to = Vector3.ProjectOnPlane(other.transform.position - transform.position, Vector3.up);
                if (to.magnitude > range + 0.3f) continue;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > arc * 0.5f) continue;
                other.ReceiveHit(damage, knockback, transform.position, this, knockdown, false, guardCrush);
            }

            // Props in the same cone shatter too (no hit-stop, the swing is not affected).
            for (int i = Breakable.All.Count - 1; i >= 0; i--)
            {
                var b = Breakable.All[i];
                if (b.IsBroken) continue;
                var to = Vector3.ProjectOnPlane(b.transform.position - transform.position, Vector3.up);
                if (to.magnitude > range + 0.3f + b.radius) continue;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > arc * 0.5f) continue;
                b.Break(transform.position);
            }
        }

        // ---------------------------------------------------------------- special

        void UpdateSpecial()
        {
            specialTimer += Time.deltaTime;
            if (special == 1)
            {
                motor.FaceOpponent();
                // Pulsing glow that speeds up as the charge fills.
                float k = specialTimer / specialChargeTime;
                if (tint) tint.SetGlow(Mathf.Lerp(0.3f, 1f, k) * (0.75f + 0.25f * Mathf.Sin(specialTimer * Mathf.Lerp(8f, 30f, k))));
                if (specialTimer < specialChargeTime) return;

                special = 2;
                specialTimer = 0f;
                if (tint) tint.SetGlow(0f);
                if (motor.DistanceToOpponent() > 1.2f) motor.AddImpulse(motor.DirectionToOpponent() * 6f);
                if (anim) anim.CrossFadeInFixedTime("SpecialAttack", 0.05f);
                return;
            }

            if (!specialHitDone && specialTimer >= specialHitTime)
            {
                specialHitDone = true;
                HitFeedback.SpecialRelease(transform.position + Vector3.up * 1f + transform.forward * 0.8f, transform.forward, new Color(1f, 0.8f, 0.25f));
                HitScan(specialBaseDamage + kit.damage, kit.range + specialRangeBonus, specialArc, specialKnockback, true, true);
            }
            if (specialTimer < specialRecoverTime) return;

            EndSpecial();
            motor.locked = false;
            if (anim) anim.CrossFadeInFixedTime("Locomotion", 0.2f);
            if (blockHeld) TryStartBlock();
        }

        void EndSpecial()
        {
            special = 0;
            specialTimer = 0f;
            if (tint) tint.SetGlow(0f);
        }

        // ---------------------------------------------------------------- being hit

        /// <param name="attacker">null for environment hazards (traps): then only the victim gets hit-stop.</param>
        /// <param name="unblockable">true = guard is ignored (e.g. spikes from below).</param>
        /// <param name="guardCrush">true = a blocked hit breaks the guard outright (special attack).</param>
        public void ReceiveHit(float damage, float knockback, Vector3 fromPosition, PlayerCombat attacker = null, bool knockdown = false, bool unblockable = false, bool guardCrush = false)
        {
            if (dead) return;
            if (dodgeTimer > dodgeDuration - dodgeInvulnerable) return; // i-frames
            if (downTimer > 0f) return;                                  // no hitting a grounded player

            var away =Vector3.ProjectOnPlane(transform.position - fromPosition, Vector3.up);
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

            bool guarded = !unblockable && blocking && Vector3.Angle(transform.forward, -away) <= blockAngle * 0.5f;
            var point = transform.position + Vector3.up * 1.2f - away * 0.35f;
            if (guarded)
            {
                HitFeedback.Hit(point, away, damage, true);
                StopTime(attacker, 0.05f);
                motor.AddImpulse(away * knockback * 0.35f);
                guardDamage += damage;
                if (guardCrush || guardDamage >= guardBreakDamage) { GuardBreak(away, knockback); return; }
                if (damage >= 20f)
                {
                    blockReactTimer = 0.45f; // big recoil from heavy weapons
                    if (anim) anim.CrossFadeInFixedTime("BlockImpact", 0.03f);
                }
                else
                {
                    blockReactTimer = kit != null && kit.blockHitClip ? kit.blockHitClip.length * 0.6f : 0.2f;
                    if (anim && kit != null && kit.blockHitClip) anim.CrossFadeInFixedTime("BlockHit", 0.03f);
                }
                return;
            }

            Health.Damage(damage);
            motor.AddImpulse(away * knockback);
            HitFeedback.Hit(point, away, damage, false);
            if (tint) tint.Flash();
            StopTime(attacker, 0.04f + damage * 0.003f);

            if (Health.IsDead)
            {
                Die();
                HitFeedback.SlowMo(0.25f, 0.6f);
                FightCamera.Shake(0.25f, 0.35f);
                return;
            }

            if (knockdown) { Knockdown(away, knockback); return; }

            // A clean hit during the wind-up cancels the special (the stamina is gone); the blow itself has armour.
            if (special == 1) EndSpecial();

            // Flinch only when not mid-swing, so a started attack is never cancelled.
            if (step < 0 && special == 0)
            {
                StopBlock(false);
                bool heavy = damage >= heavyHitDamage;
                stunTimer = heavy ? heavyStunTime : stunTime;
                motor.locked = true;
                if (anim) anim.CrossFadeInFixedTime(heavy ? "HitHeavy" : "Hit", 0.05f);
            }
        }

        /// <summary>Combo finisher: thrown to the ground, then gets up. Interrupts any action.</summary>
        void Knockdown(Vector3 away, float knockback)
        {
            step = -1;
            queued = false;
            EndSpecial();
            ClearUpper();
            StopBlock(false);
            stunTimer = 0f;
            motor.attacking = false;
            motor.locked = true;
            motor.AddImpulse(away * knockback * 0.6f);
            downTimer = knockdownLieTime + getUpTime;
            gettingUp = false;
            FightCamera.Shake(0.2f, 0.3f);
            if (anim) anim.CrossFadeInFixedTime("KnockedDown", 0.05f);
        }

        /// <summary>Too much blocked damage: guard drops and the player is stunned.</summary>
        void GuardBreak(Vector3 away, float knockback)
        {
            guardDamage = 0f;
            EndSpecial();
            StopBlock(false);
            stunTimer = guardBreakStun;
            motor.locked = true;
            motor.AddImpulse(away * knockback * 0.5f);
            FightCamera.Shake(0.15f, 0.25f);
            if (anim) anim.CrossFadeInFixedTime("Stunned", 0.05f);
        }

        /// <summary>Round won: celebrate until the next ResetFighter.</summary>
        public void Celebrate()
        {
            if (dead) return;
            step = -1;
            queued = false;
            EndSpecial();
            ClearUpper();
            StopBlock(false);
            celebrating = true;
            motor.attacking = false;
            motor.locked = true;
            if (anim) anim.CrossFadeInFixedTime("Victory", 0.25f);
        }

        /// <summary>Brief freeze of both fighters' animation on impact ("hit stop").</summary>
        void StopTime(PlayerCombat attacker, float duration)
        {
            HitStop(duration);
            if (attacker) attacker.HitStop(duration);
        }

        public void HitStop(float duration)
        {
            hitStopTimer = Mathf.Max(hitStopTimer, duration);
            if (anim) anim.speed = 0f;
        }

        void Die()
        {
            dead = true;
            step = -1;
            queued = false;
            EndSpecial();
            ClearUpper();
            blocking = false;
            motor.attacking = false;
            motor.blocking = false;
            motor.locked = true;
            if (anim) anim.CrossFadeInFixedTime("Death", 0.1f);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (hitStopTimer > 0f)
            {
                hitStopTimer -= Time.unscaledDeltaTime;
                if (hitStopTimer <= 0f) { hitStopTimer = 0f; if (anim) anim.speed = 1f; }
                return; // gameplay timers pause while frozen
            }

            if (dead || celebrating || kit == null) return;

            if (hasUpperLayer)
            {
                upperWeight = Mathf.MoveTowards(upperWeight, step >= 0 ? 1f : 0f, 6f * Time.deltaTime);
                anim.SetLayerWeight(1, upperWeight);
            }

            if (dodgeCooldownTimer > 0f) dodgeCooldownTimer -= Time.deltaTime;
            if (special == 0) stamina = Mathf.Min(maxStamina, stamina + staminaRegen * Time.deltaTime);
            if (guardDamage > 0f && !blocking) guardDamage = Mathf.Max(0f, guardDamage - guardRecovery * Time.deltaTime);

            if (downTimer > 0f)
            {
                downTimer -= Time.deltaTime;
                if (!gettingUp && downTimer <= getUpTime)
                {
                    gettingUp = true;
                    if (anim) anim.CrossFadeInFixedTime("GettingUp", 0.15f);
                }
                if (downTimer <= 0f)
                {
                    downTimer = 0f;
                    gettingUp = false;
                    motor.locked = false;
                    if (anim) anim.CrossFadeInFixedTime("Locomotion", 0.2f);
                    if (blockHeld) TryStartBlock();
                }
                return;
            }

            if (dodgeTimer > 0f)
            {
                dodgeTimer -= Time.deltaTime;
                if (dodgeTimer <= 0f)
                {
                    dodgeTimer = 0f;
                    dodgeCooldownTimer = dodgeCooldown;
                    motor.locked = false;
                    if (anim) anim.CrossFadeInFixedTime("Locomotion", blendTime);
                    if (blockHeld) TryStartBlock();
                }
                return;
            }

            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                if (stunTimer <= 0f)
                {
                    motor.locked = false;
                    if (anim) anim.CrossFadeInFixedTime("Locomotion", blendTime);
                    if (blockHeld) TryStartBlock();
                }
                return;
            }

            if (special > 0) { UpdateSpecial(); return; }

            if (blockReactTimer > 0f)
            {
                blockReactTimer -= Time.deltaTime;
                if (blockReactTimer <= 0f && blocking && anim) anim.CrossFadeInFixedTime("Block", blendTime);
            }

            if (blocking && !blockHeld) StopBlock(true);

            if (step < 0)
            {
                if (blockHeld) TryStartBlock();
                return;
            }

            float len = kit.comboClips[step].length / Mathf.Max(0.01f, kit.attackSpeed);
            time += Time.deltaTime;

            // Legs: follow the stick while moving, join the full-body swing when standing still.
            if (hasUpperLayer)
            {
                bool moving = motor.HasMoveInput;
                if (moving && baseInAttack)
                {
                    baseInAttack = false;
                    anim.CrossFadeInFixedTime("Locomotion", 0.15f, 0);
                }
                else if (!moving && !baseInAttack && time < len * 0.85f)
                {
                    baseInAttack = true;
                    anim.CrossFadeInFixedTime("Attack" + step, 0.15f, 0, time * kit.attackSpeed);
                }
            }

            if (!hitDone && time / len >= kit.hitStart)
            {
                hitDone = true;
                DoHit();
            }

            if (time < len) return;

            // Clip finished: chain the queued attack or go back to locomotion.
            if (queued) { Begin((step + 1) % kit.comboClips.Length); return; }
            step = -1;
            motor.attacking = false;
            if (anim && baseInAttack) anim.CrossFadeInFixedTime("Locomotion", blendTime);
            baseInAttack = false;
            if (blockHeld) TryStartBlock();
        }
    }
}
