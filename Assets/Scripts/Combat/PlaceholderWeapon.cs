using UnityEngine;

/// <summary>
/// Swings a placeholder weapon pivot based on the attack phase so combat is readable
/// before real animations exist. Remove once the animated models are in.
/// </summary>
public class PlaceholderWeapon : MonoBehaviour
{
    public CombatController combat;
    public Transform pivot;
    public float raiseAngle = -100f;
    public float swingAngle = 60f;

    void Update()
    {
        if (combat == null || pivot == null) return;

        float angle = 0f;
        if (combat.State == CombatState.Attacking)
        {
            switch (combat.Phase)
            {
                case AttackPhase.Windup:
                    angle = Mathf.Lerp(0f, raiseAngle, combat.PhaseProgress);
                    break;
                case AttackPhase.Active:
                    angle = Mathf.Lerp(raiseAngle, swingAngle, combat.PhaseProgress);
                    break;
                case AttackPhase.Recovery:
                    angle = Mathf.Lerp(swingAngle, 0f, combat.PhaseProgress);
                    break;
            }
        }
        else if (combat.State == CombatState.Blocking)
        {
            angle = -60f;
        }

        pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
