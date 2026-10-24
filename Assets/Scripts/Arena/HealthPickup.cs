using System.Collections.Generic;
using UnityEngine;

namespace KitFight
{
    /// <summary>
    /// Health potion dropped by some breakables. The first living, hurt fighter that walks over it is healed.
    /// Spawned at runtime by Breakable; GameManager clears all of them at the start of every round.
    /// </summary>
    public class HealthPickup : MonoBehaviour
    {
        public static readonly List<HealthPickup> All = new List<HealthPickup>();

        public float amount = 20f;
        public float pickupRadius = 0.9f;
        public float lifetime = 12f;
        public float spawnDelay = 0.35f;    // can't be grabbed while it pops out of the debris
        public float bobHeight = 0.12f;
        public float spinSpeed = 120f;

        Transform visual;
        float age;
        const float BaseY = 0.55f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Creates a pickup at pos; uses the potion model if given, otherwise a glowing sphere.</summary>
        public static HealthPickup Spawn(Vector3 pos, float amount, GameObject model)
        {
            var root = new GameObject("HealthPickup");
            root.transform.position = pos;
            var p = root.AddComponent<HealthPickup>();
            p.amount = amount;

            GameObject v;
            if (model) v = Instantiate(model, root.transform);
            else
            {
                v = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                v.transform.SetParent(root.transform, false);
                v.transform.localScale = Vector3.one * 0.35f;
                v.GetComponent<Renderer>().material.color = new Color(0.3f, 1f, 0.45f);
            }
            foreach (var c in v.GetComponentsInChildren<Collider>()) Destroy(c);
            v.transform.localPosition = Vector3.up * BaseY;
            p.visual = v.transform;

            // Soft green light so the drop reads from the fight camera.
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Vector3.up * 0.8f;
            light.type = LightType.Point;
            light.color = new Color(0.3f, 1f, 0.45f);
            light.range = 2.5f;
            light.intensity = 2f;
            return p;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (visual)
            {
                visual.localPosition = new Vector3(0f, BaseY + Mathf.Sin(age * 3f) * bobHeight, 0f);
                visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }
            if (age > lifetime) { Destroy(gameObject); return; }
            if (age < spawnDelay || !GameManager.IsFighting) return;

            foreach (var pc in PlayerCombat.All)
            {
                if (pc.IsDead || pc.Health.Fraction >= 1f) continue;
                var d = Vector3.ProjectOnPlane(pc.transform.position - transform.position, Vector3.up);
                if (d.magnitude > pickupRadius) continue;
                pc.Health.Heal(amount);
                HitFeedback.Heal(transform.position + Vector3.up * 0.6f);
                Destroy(gameObject);
                return;
            }
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
        }
    }
}
