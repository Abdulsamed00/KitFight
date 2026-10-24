using UnityEngine;

namespace KitFight
{
    /// <summary>Gives each player's body a team colour and can flash white when hit.</summary>
    public class PlayerTint : MonoBehaviour
    {
        public Color color = Color.white;

        Material[] mats;
        float flashTimer, flashDuration = 0.12f;

        void Awake()
        {
            var list = new System.Collections.Generic.List<Material>();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
                list.Add(r.material); // instance
            mats = list.ToArray();
            Apply(color);
        }

        public void Flash(float duration = 0.14f)
        {
            flashDuration = Mathf.Max(0.01f, duration);
            flashTimer = flashDuration;
        }

        /// <summary>0..1 golden glow while a special attack charges (set every frame, 0 = off).</summary>
        public void SetGlow(float amount) => glow = Mathf.Clamp01(amount);

        static readonly Color GlowColor = new Color(1f, 0.8f, 0.25f);
        float glow, appliedGlow;

        void Update()
        {
            if (flashTimer <= 0f && glow == appliedGlow) return;
            if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
            appliedGlow = glow;
            var c = Color.Lerp(color, GlowColor, glow);
            Apply(Color.Lerp(c, Color.white, Mathf.Clamp01(flashTimer / flashDuration)));
        }

        void Apply(Color c)
        {
            foreach (var m in mats)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
        }
    }
}
