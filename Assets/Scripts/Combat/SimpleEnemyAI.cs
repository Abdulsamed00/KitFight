using UnityEngine;

/// <summary>Minimal chase-and-attack dummy used to test combat.</summary>
[RequireComponent(typeof(CombatController), typeof(CharacterMotor))]
public class SimpleEnemyAI : MonoBehaviour
{
    public float sightRange = 15f;
    public float attackInterval = 1.5f;
    [Range(0f, 1f)] public float heavyChance = 0.25f;
    [Range(0f, 1f)] public float blockChance = 0.2f;

    CombatController combat;
    CharacterMotor motor;
    Transform target;
    float attackTimer;

    void Awake()
    {
        combat = GetComponent<CombatController>();
        motor = GetComponent<CharacterMotor>();
        attackTimer = Random.Range(0.5f, attackInterval);
    }

    void Update()
    {
        if (combat.State == CombatState.Dead || combat.kit == null)
        {
            motor.SetMove(Vector3.zero);
            return;
        }

        if (target == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) target = player.transform;
            else return;
        }

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

        if (dist > sightRange)
        {
            motor.SetMove(Vector3.zero);
            combat.SetBlock(false);
            return;
        }

        float reach = combat.kit.lightCombo != null && combat.kit.lightCombo.Length > 0
            ? combat.kit.lightCombo[0].range * 0.8f
            : 1.5f;

        bool free = combat.State == CombatState.Idle || combat.State == CombatState.Blocking;
        if (free)
        {
            motor.FaceTowards(dir);
            motor.SetMove(dist > reach ? dir : Vector3.zero);
        }
        else
        {
            motor.SetMove(Vector3.zero);
        }

        attackTimer -= Time.deltaTime;
        if (dist <= reach && attackTimer <= 0f && free)
        {
            if (Random.value < blockChance) combat.SetBlock(true);
            else
            {
                combat.SetBlock(false);
                if (Random.value < heavyChance && combat.Heavy()) { }
                else combat.Light();
            }
            attackTimer = attackInterval * Random.Range(0.7f, 1.3f);
        }
        else if (combat.State == CombatState.Blocking && attackTimer < attackInterval * 0.5f)
        {
            combat.SetBlock(false);
        }
    }
}
