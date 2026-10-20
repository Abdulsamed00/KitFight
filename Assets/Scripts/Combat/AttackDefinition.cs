using System;
using UnityEngine;

[Serializable]
public class AttackDefinition
{
    public string displayName = "Attack";
    public float damage = 10f;

    [Header("Timing (seconds)")]
    public float windup = 0.2f;
    public float active = 0.15f;
    public float recovery = 0.3f;

    [Header("Reach")]
    public float range = 2f;
    [Range(10f, 360f)] public float arc = 100f;

    [Header("Effects")]
    public float knockback = 3f;
    public float stun = 0.25f;
    public float staminaCost = 10f;
    public float lungeDistance = 0.8f;
    public bool unblockable;

    [Header("Animation")]
    public string animTrigger;
}
