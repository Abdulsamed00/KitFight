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

        void Update()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.unscaledDeltaTime;
            Apply(Color.Lerp(color, Color.white, Mathf.Clamp01(flashTimer / flashDuration)));
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
