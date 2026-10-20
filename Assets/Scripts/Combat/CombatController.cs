using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatState { Idle, Attacking, Blocking, Dodging, Stunned, Dead }
public enum AttackPhase { None, Windup, Active, Recovery }

/// <summary>
/// Input-agnostic combat state machine. Players and AI both drive it through the
/// command methods (Light, Heavy, UseAbility, Dodge, SetBlock) so the same logic
/// can later run server-side.
/// </summary>
[RequireComponent(typeof(Health), typeof(CharacterMotor))]
public class CombatController : MonoBehaviour
{
    public KitDefinition kit;
    public LayerMask hitMask = ~0;
    public Animator animator;
    public Renderer[] tintRenderers;
    [Tooltip("Seconds after death before respawning at the start position. 0 = never.")]
    public float respawnDelay;

    public Health Health { get; private set; }
    public CharacterMotor Motor { get; private set; }

    public CombatState State { get; private set; }
    public AttackPhase Phase { get; private set; }
    public float PhaseProgress { get; private set; }
    public AttackDefinition CurrentAttack { get; private set; }
    public float Stamina { get; private set; }
    public float AbilityCooldownLeft { get; private set; }

    public event Action<AttackDefinition> AttackStarted;
    public event Action<CombatController, HitResult> HitLanded;
    public event Action<HitResult, DamageInfo> HitReceived;
    public event Action<KitDefinition> KitApplied;

    static readonly Collider[] overlapBuffer = new Collider[32];
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly HashSet<CombatController> alreadyHit = new HashSet<CombatController>();
    MaterialPropertyBlock block;
    Vector3 spawnPosition;
    Quaternion spawnRotation;
    GameObject spawnedModel;

    float phaseTime;
    float stateTime;
    float stunTime;
    float comboTimer;
    int nextCombo;
    bool queuedLight;
    bool blockHeld;

    void Awake()
    {
        Health = GetComponent<Health>();
        Motor = GetComponent<CharacterMotor>();
        block = new MaterialPropertyBlock();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        Health.Died += OnDied;
    }

    void Start()
    {
        if (kit != null) ApplyKit(kit);
    }

    void OnDestroy()
    {
        if (Health != null) Health.Died -= OnDied;
    }

    public void ApplyKit(KitDefinition newKit)
    {
        kit = newKit;
        Health.SetMax(kit.maxHealth);
        Stamina = kit.maxStamina;
        Motor.moveSpeed = kit.moveSpeed;
        AbilityCooldownLeft = 0f;
        nextCombo = 0;
        ReturnToIdle();

        if (spawnedModel != null) Destroy(spawnedModel);
        if (kit.modelPrefab != null)
        {
            spawnedModel = Instantiate(kit.modelPrefab, transform);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
        }

        KitApplied?.Invoke(kit);
    }

    // ---------- Commands ----------

    public bool Light()
    {
        if (kit == null || kit.lightCombo == null || kit.lightCombo.Length == 0) return false;
        if (State == CombatState.Attacking)
        {
            queuedLight = true;
            return true;
        }
        if (State != CombatState.Idle && State != CombatState.Blocking) return false;
        return StartAttack(kit.lightCombo[nextCombo], true);
    }

    public bool Heavy()
    {
        if (kit == null || kit.heavy == null) return false;
        if (State != CombatState.Idle && State != CombatState.Blocking) return false;
        return StartAttack(kit.heavy, false);
    }

    public bool UseAbility()
    {
        if (kit == null || kit.ability == null || AbilityCooldownLeft > 0f) return false;
        if (State != CombatState.Idle && State != CombatState.Blocking) return false;
        if (!StartAttack(kit.ability, false)) return false;
        AbilityCooldownLeft = kit.abilityCooldown;
        return true;
    }

    public bool Dodge(Vector3 direction)
    {
        if (kit == null) return false;
        bool canCancel = State == CombatState.Attacking && Phase == AttackPhase.Recovery;
        if (State != CombatState.Idle && State != CombatState.Blocking && !canCancel) return false;
        if (Stamina < kit.dodgeStaminaCost) return false;

        Stamina -= kit.dodgeStaminaCost;
        queuedLight = false;
        CurrentAttack = null;
        Phase = AttackPhase.None;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
        Motor.CancelDash();
        Motor.FaceTowards(direction, true);
        Motor.Dash(direction, kit.dodgeDistance, kit.dodgeDuration);
        SetState(CombatState.Dodging);
        Trigger("Dodge");
        return true;
    }

    public void SetBlock(bool held)
    {
        blockHeld = held;
        if (held && State == CombatState.Idle) SetState(CombatState.Blocking);
        else if (!held && State == CombatState.Blocking) SetState(CombatState.Idle);
    }

    public float AbilityCooldownNormalized =>
        kit != null && kit.abilityCooldown > 0f ? Mathf.Clamp01(AbilityCooldownLeft / kit.abilityCooldown) : 0f;

    // ---------- Damage ----------

    public HitResult ReceiveHit(DamageInfo info)
    {
        if (State == CombatState.Dead) return HitResult.Ignored;

        if (State == CombatState.Dodging && stateTime < kit.dodgeInvulnerable)
        {
            HitReceived?.Invoke(HitResult.Dodged, info);
            return HitResult.Dodged;
        }

        if (State == CombatState.Blocking && !info.unblockable && IsFacing(info.source))
        {
            float staminaCost = info.amount * kit.blockStaminaPerDamage;
            if (Stamina >= staminaCost)
            {
                Stamina -= staminaCost;
                Health.Damage(info.amount * (1f - kit.blockDamageReduction));
                Motor.Knockback(info.direction * info.knockback * 0.3f);
                HitReceived?.Invoke(HitResult.Blocked, info);
                return HitResult.Blocked;
            }

            Stamina = 0f;
            Health.Damage(info.amount);
            if (!Health.IsDead) Stagger(1f);
            Motor.Knockback(info.direction * info.knockback);
            HitReceived?.Invoke(HitResult.GuardBroken, info);
            return HitResult.GuardBroken;
        }

        Health.Damage(info.amount);
        if (!Health.IsDead)
        {
            if (info.stun > 0f) Stagger(info.stun);
            Motor.Knockback(info.direction * info.knockback);
        }
        HitReceived?.Invoke(HitResult.Hit, info);
        return HitResult.Hit;
    }

    bool IsFacing(CombatController other)
    {
        if (other == null) return true;
        Vector3 to = other.transform.position - transform.position;
        to.y = 0f;
        return Vector3.Dot(transform.forward, to.normalized) > 0.3f;
    }

    void Stagger(float seconds)
    {
        queuedLight = false;
        CurrentAttack = null;
        Phase = AttackPhase.None;
        Motor.CancelDash();
        stunTime = seconds;
        SetState(CombatState.Stunned);
        Trigger("Hit");
    }

    void OnDied()
    {
        queuedLight = false;
        CurrentAttack = null;
        Phase = AttackPhase.None;
        Motor.CancelDash();
        Motor.SetMove(Vector3.zero);
        SetState(CombatState.Dead);
        Trigger("Die");
        if (respawnDelay > 0f) Invoke(nameof(Respawn), respawnDelay);
    }

    void Respawn()
    {
        var cc = GetComponent<CharacterController>();
        cc.enabled = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        cc.enabled = true;
        Health.Revive();
        if (kit != null) Stamina = kit.maxStamina;
        ReturnToIdle();
    }

    // ---------- Attacks ----------

    bool StartAttack(AttackDefinition attack, bool isLight)
    {
        if (Stamina < attack.staminaCost) return false;
        Stamina -= attack.staminaCost;

        CurrentAttack = attack;
        alreadyHit.Clear();
        queuedLight = false;
        Phase = AttackPhase.Windup;
        phaseTime = 0f;
        PhaseProgress = 0f;
        SetState(CombatState.Attacking);

        if (isLight) nextCombo = (nextCombo + 1) % kit.lightCombo.Length;
        if (!string.IsNullOrEmpty(attack.animTrigger)) Trigger(attack.animTrigger);

        AttackStarted?.Invoke(attack);
        return true;
    }

    void UpdateAttack(float dt)
    {
        var a = CurrentAttack;
        phaseTime += dt;

        switch (Phase)
        {
            case AttackPhase.Windup:
                PhaseProgress = a.windup > 0f ? Mathf.Clamp01(phaseTime / a.windup) : 1f;
                if (phaseTime >= a.windup)
                {
                    Phase = AttackPhase.Active;
                    phaseTime = 0f;
                    Motor.Dash(transform.forward, a.lungeDistance, Mathf.Max(a.active, 0.05f));
                }
                break;

            case AttackPhase.Active:
                PhaseProgress = a.active > 0f ? Mathf.Clamp01(phaseTime / a.active) : 1f;
                ScanHits(a);
                if (phaseTime >= a.active)
                {
                    Phase = AttackPhase.Recovery;
                    phaseTime = 0f;
                }
                break;

            case AttackPhase.Recovery:
                PhaseProgress = a.recovery > 0f ? Mathf.Clamp01(phaseTime / a.recovery) : 1f;
                if (queuedLight && PhaseProgress >= kit.comboCancelFraction)
                {
                    StartAttack(kit.lightCombo[nextCombo], true);
                }
                else if (phaseTime >= a.recovery)
                {
                    comboTimer = kit.comboResetTime;
                    ReturnToIdle();
                }
                break;
        }
    }

    void ScanHits(AttackDefinition a)
    {
        Vector3 origin = transform.position + Vector3.up;
        int count = Physics.OverlapSphereNonAlloc(origin, a.range, overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);
        float halfArcCos = Mathf.Cos(a.arc * 0.5f * Mathf.Deg2Rad);

        for (int i = 0; i < count; i++)
        {
            var target = overlapBuffer[i].GetComponentInParent<CombatController>();
            if (target == null || target == this || alreadyHit.Contains(target)) continue;
            if (target.Health.team == Health.team || target.Health.IsDead) continue;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            if (a.arc < 359f && Vector3.Dot(transform.forward, dir) < halfArcCos) continue;

            alreadyHit.Add(target);
            var info = new DamageInfo
            {
                amount = a.damage * kit.damageMultiplier,
                direction = dir,
                knockback = a.knockback,
                stun = a.stun,
                unblockable = a.unblockable,
                source = this
            };
            HitLanded?.Invoke(target, target.ReceiveHit(info));
        }
    }

    // ---------- State ----------

    void Update()
    {
        float dt = Time.deltaTime;
        if (kit == null || State == CombatState.Dead) { UpdateTint(); return; }

        stateTime += dt;
        if (AbilityCooldownLeft > 0f) AbilityCooldownLeft = Mathf.Max(0f, AbilityCooldownLeft - dt);

        switch (State)
        {
            case CombatState.Attacking:
                UpdateAttack(dt);
                break;

            case CombatState.Dodging:
                if (stateTime >= kit.dodgeDuration) ReturnToIdle();
                break;

            case CombatState.Stunned:
                stunTime -= dt;
                if (stunTime <= 0f) ReturnToIdle();
                break;

            case CombatState.Idle:
            case CombatState.Blocking:
                Stamina = Mathf.Min(kit.maxStamina, Stamina + kit.staminaRegen * (State == CombatState.Blocking ? 0.4f : 1f) * dt);
                if (comboTimer > 0f)
                {
                    comboTimer -= dt;
                    if (comboTimer <= 0f) nextCombo = 0;
                }
                break;
        }

        Motor.speedMultiplier = State switch
        {
            CombatState.Attacking => Phase == AttackPhase.Recovery ? 0.5f : 0.15f,
            CombatState.Blocking => 0.5f,
            CombatState.Dodging => 0f,
            CombatState.Stunned => 0f,
            CombatState.Dead => 0f,
            _ => 1f
        };

        UpdateTint();
    }

    void ReturnToIdle()
    {
        CurrentAttack = null;
        Phase = AttackPhase.None;
        PhaseProgress = 0f;
        queuedLight = false;
        SetState(blockHeld ? CombatState.Blocking : CombatState.Idle);
    }

    void SetState(CombatState next)
    {
        State = next;
        stateTime = 0f;
    }

    void Trigger(string trigger)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
            {
                animator.SetTrigger(trigger);
                return;
            }
        }
    }

    void UpdateTint()
    {
        if (tintRenderers == null || tintRenderers.Length == 0) return;

        Color baseColor = kit != null ? kit.color : Color.white;
        Color c = State switch
        {
            CombatState.Attacking when Phase == AttackPhase.Windup => Color.Lerp(baseColor, Color.yellow, 0.7f),
            CombatState.Attacking when Phase == AttackPhase.Active => Color.red,
            CombatState.Blocking => new Color(0.3f, 0.5f, 1f),
            CombatState.Dodging => new Color(0.3f, 1f, 1f),
            CombatState.Stunned => Color.gray,
            CombatState.Dead => new Color(0.15f, 0.15f, 0.15f),
            _ => baseColor
        };

        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        foreach (var r in tintRenderers)
            if (r != null) r.SetPropertyBlock(block);
    }

    void OnDrawGizmosSelected()
    {
        var a = CurrentAttack ?? (kit != null && kit.lightCombo != null && kit.lightCombo.Length > 0 ? kit.lightCombo[0] : null);
        if (a == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, a.range);
    }
}
