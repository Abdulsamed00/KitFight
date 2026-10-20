using UnityEngine;

[CreateAssetMenu(menuName = "KitFight/Kit", fileName = "NewKit")]
public class KitDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Kit";
    [TextArea] public string description;
    public Color color = Color.white;
    public Sprite icon;
    [Tooltip("Optional visual model; spawned under the character when the kit is applied.")]
    public GameObject modelPrefab;

    [Header("Stats")]
    public float maxHealth = 100f;
    public float maxStamina = 100f;
    public float staminaRegen = 20f;
    public float moveSpeed = 5f;
    public float damageMultiplier = 1f;

    [Header("Attacks")]
    public AttackDefinition[] lightCombo;
    public AttackDefinition heavy;
    [Range(0.2f, 1f)] public float comboCancelFraction = 0.6f;
    public float comboResetTime = 0.8f;

    [Header("Ability")]
    public string abilityName = "Ability";
    public float abilityCooldown = 8f;
    public AttackDefinition ability;

    [Header("Defense")]
    [Range(0f, 1f)] public float blockDamageReduction = 0.8f;
    public float blockStaminaPerDamage = 0.5f;
    public float dodgeDistance = 4f;
    public float dodgeDuration = 0.35f;
    public float dodgeInvulnerable = 0.25f;
    public float dodgeStaminaCost = 20f;
}
