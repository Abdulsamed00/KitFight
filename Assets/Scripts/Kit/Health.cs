using UnityEngine;

namespace KitFight
{
    public class Health : MonoBehaviour
    {
        public float max = 100f;

        public float Current { get; private set; }
        public float Fraction => Mathf.Clamp01(Current / max);
        public bool IsDead => Current <= 0f;

        void Awake() => Current = max;

        public void Damage(float amount) => Current = Mathf.Max(0f, Current - amount);

        public void Heal(float amount)
        {
            if (!IsDead) Current = Mathf.Min(max, Current + amount);
        }

        public void ResetHealth() => Current = max;
    }
}
