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
        public float stunTime = 0.5f;

        public bool IsAttacking => step >= 0;
        public bool IsBlocking => blocking;
        public bool IsDead => dead;
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

        static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            motor = GetComponent<PlayerMotor>();
            Health = GetComponent<Health>();
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
            blockHeld = false;
            Health.ResetHealth();
            if (motor) { motor.locked = false; motor.ClearImpulse(); }
        }

        void ClearActions()
        {
            step = -1;
            queued = hitDone = false;
            blocking = false;
            if (motor) { motor.attacking = false; motor.blocking = false; }
            if (anim && anim.runtimeAnimatorController) anim.CrossFadeInFixedTime("Locomotion", 0f);
        }

        // ---------------------------------------------------------------- commands

        public void Attack()
        {
            if (dead || stunTimer > 0f || blocking) return;
            if (kit == null || kit.comboClips == null || kit.comboClips.Length == 0) return;
            if (step < 0) Begin(0);
            else queued = true;
        }

        public void SetBlock(bool held)
        {
            blockHeld = held;
            if (held) TryStartBlock();
        }

        // ---------------------------------------------------------------- block

        void TryStartBlock()
        {
            if (dead || blocking || step >= 0 || stunTimer > 0f || kit == null || kit.blockClip == null) return;
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
            if (motor) { motor.attacking = true; motor.FaceOpponent(); }
            if (!anim) return;
            anim.SetFloat(AttackSpeedHash, kit.attackSpeed);
            anim.CrossFadeInFixedTime("Attack" + i, blendTime, 0, 0f);
        }

        void DoHit()
        {
            foreach (var other in All)
            {
                if (other == this || other.dead) continue;
                var to = Vector3.ProjectOnPlane(other.transform.position - transform.position, Vector3.up);
                if (to.magnitude > kit.range + 0.3f) continue;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > kit.arc * 0.5f) continue;
                other.ReceiveHit(kit.damage, kit.knockback, transform.position);
            }
        }

        // ---------------------------------------------------------------- being hit

        public void ReceiveHit(float damage, float knockback, Vector3 fromPosition)
        {
            if (dead) return;

            var away = Vector3.ProjectOnPlane(transform.position - fromPosition, Vector3.up);
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

            bool guarded = blocking && Vector3.Angle(transform.forward, -away) <= blockAngle * 0.5f;
            if (guarded)
            {
                motor.AddImpulse(away * knockback * 0.35f);
                blockReactTimer = kit != null && kit.blockHitClip ? kit.blockHitClip.length * 0.8f : 0.2f;
                if (anim && kit != null && kit.blockHitClip) anim.CrossFadeInFixedTime("BlockHit", 0.03f);
                return;
            }

            Health.Damage(damage);
            motor.AddImpulse(away * knockback);

            if (Health.IsDead) { Die(); return; }

            // Flinch only when not mid-swing, so a started attack is never cancelled.
            if (step < 0)
            {
                StopBlock(false);
                stunTimer = stunTime;
                motor.locked = true;
                if (anim) anim.CrossFadeInFixedTime("Hit", 0.05f);
            }
        }

        void Die()
        {
            dead = true;
            step = -1;
            queued = false;
            blocking = false;
            motor.attacking = false;
            motor.blocking = false;
            motor.locked = true;
            if (anim) anim.CrossFadeInFixedTime("Death", 0.1f);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (dead || kit == null) return;

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
            if (anim) anim.CrossFadeInFixedTime("Locomotion", blendTime);
            if (blockHeld) TryStartBlock();
        }
    }
}
