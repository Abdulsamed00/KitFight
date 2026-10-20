using System;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    public static readonly List<Health> All = new List<Health>();

    public int team;
    public float max = 100f;

    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    public event Action<float> Changed;
    public event Action Died;

    void Awake() => Current = max;
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void SetMax(float value, bool refill = true)
    {
        max = value;
        Current = refill ? max : Mathf.Min(Current, max);
        Changed?.Invoke(Current);
    }

    public void Damage(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Max(0f, Current - amount);
        Changed?.Invoke(Current);
        if (Current <= 0f) Died?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(max, Current + amount);
        Changed?.Invoke(Current);
    }

    public void Revive()
    {
        Current = max;
        Changed?.Invoke(Current);
    }
}
