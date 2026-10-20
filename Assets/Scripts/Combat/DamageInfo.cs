using UnityEngine;

public enum HitResult { Ignored, Hit, Blocked, GuardBroken, Dodged }

public struct DamageInfo
{
    public float amount;
    public Vector3 direction;
    public float knockback;
    public float stun;
    public bool unblockable;
    public CombatController source;
}
