using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KitFight
{
    /// <summary>
    /// Prop that shatters when an attack's hit scan reaches it: no damage, no hit-stop/shake, and it never
    /// interrupts the attacker. Props with healAmount or staminaAmount > 0 drop a Pickup potion when broken.
    /// Commands: Break(fromPosition), ResetObject(). GameManager resets all of them every round.
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        public static readonly List<Breakable> All = new List<Breakable>();

        [Tooltip("Intact model holder (hidden when broken). The broken model spawns with the same local transform.")]
        public GameObject intactVisual;
        [Tooltip("Broken model; every child mesh becomes a debris piece. Empty = cubes are generated.")]
        public GameObject brokenPrefab;
        [Tooltip("Rough footprint radius, added to the attacker's reach.")]
        public float radius = 0.5f;
        public float explosionForce = 3.5f;
        public float upwardForce = 2f;
        public float debrisLifetime = 2.2f;
        public float shrinkTime = 0.5f;

        [Header("Loot")]
        [Tooltip("Health restored by the potion this prop drops; 0 = no health potion.")]
        public float healAmount;
        [Tooltip("Stamina restored by the potion this prop drops; 0 = no stamina potion.")]
        public float staminaAmount;
        [Tooltip("Potion model for the pickup. Empty = a glowing sphere.")]
        public GameObject potionModel;

        public bool IsBroken { get; private set; }

        Collider solid;
        GameObject debris;

        void Awake() => solid = GetComponent<Collider>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Break(Vector3 fromPosition)
        {
            if (IsBroken) return;
            IsBroken = true;
            if (solid) solid.enabled = false;
            if (intactVisual) intactVisual.SetActive(false);

            debris = new GameObject("Debris");
            debris.transform.SetParent(transform, false);

            var pieces = new List<Transform>();
            if (brokenPrefab) SpawnPrefabPieces(pieces);
            else SpawnCubePieces(pieces);

            var center = transform.position + Vector3.up * 0.4f;
            var away = Vector3.ProjectOnPlane(transform.position - fromPosition, Vector3.up);
            away = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.zero;

            foreach (var p in pieces)
            {
                var col = p.GetComponent<Collider>();
                if (!col) col = p.gameObject.AddComponent<BoxCollider>();
                IgnorePlayers(col);

                var rb = p.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                var outward = p.position - center;
                outward = outward.sqrMagnitude > 0.001f ? outward.normalized : Random.onUnitSphere;
                var v = (outward + away * 1.2f).normalized * explosionForce * Random.Range(0.7f, 1.2f)
                        + Vector3.up * upwardForce * Random.Range(0.6f, 1.2f);
                rb.AddForce(v, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
            }

            StopAllCoroutines();
            StartCoroutine(FadeDebris(pieces));

            if (healAmount > 0f) Pickup.Spawn(transform.position, Pickup.Kind.Health, healAmount, potionModel);
            else if (staminaAmount > 0f) Pickup.Spawn(transform.position, Pickup.Kind.Stamina, staminaAmount, potionModel);
        }

        public void ResetObject()
        {
            StopAllCoroutines();
            if (debris) Destroy(debris);
            debris = null;
            IsBroken = false;
            if (intactVisual) intactVisual.SetActive(true);
            if (solid) solid.enabled = true;
        }

        void SpawnPrefabPieces(List<Transform> pieces)
        {
            // The debris root copies the intact visual's holder transform (fit offset/scale/rotation);
            // the model keeps its own import transform underneath.
            if (intactVisual)
            {
                var v = intactVisual.transform;
                debris.transform.SetLocalPositionAndRotation(v.localPosition, v.localRotation);
                debris.transform.localScale = v.localScale;
            }
            var inst = Instantiate(brokenPrefab, debris.transform);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
                pieces.Add(r.transform);
        }

        void SpawnCubePieces(List<Transform> pieces)
        {
            // Fallback: a few small cubes filling the intact bounds, using its material.
            Material mat = null;
            var bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
            if (intactVisual)
            {
                var rends = intactVisual.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    mat = rends[0].sharedMaterial;
                    bounds = rends[0].bounds;
                    foreach (var r in rends) bounds.Encapsulate(r.bounds);
                }
            }
            float size = Mathf.Max(0.08f, Mathf.Min(bounds.size.x, bounds.size.y, bounds.size.z) * 0.35f);
            var e = bounds.extents * 0.6f;
            for (int i = 0; i < 8; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Piece" + i;
                c.transform.SetParent(debris.transform, true);
                c.transform.position = bounds.center + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                c.transform.rotation = Random.rotation;
                c.transform.localScale = Vector3.one * size * Random.Range(0.7f, 1.2f);
                if (mat) c.GetComponent<Renderer>().sharedMaterial = mat;
                pieces.Add(c.transform);
            }
        }

        static void IgnorePlayers(Collider col)
        {
            foreach (var pc in PlayerCombat.All)
            {
                var cc = pc.GetComponent<CharacterController>();
                if (cc) Physics.IgnoreCollision(col, cc, true);
            }
        }

        IEnumerator FadeDebris(List<Transform> pieces)
        {
            yield return new WaitForSeconds(debrisLifetime);
            var start = new Vector3[pieces.Count];
            for (int i = 0; i < pieces.Count; i++) if (pieces[i]) start[i] = pieces[i].localScale;
            for (float t = 0f; t < shrinkTime; t += Time.deltaTime)
            {
                float k = 1f - t / shrinkTime;
                for (int i = 0; i < pieces.Count; i++) if (pieces[i]) pieces[i].localScale = start[i] * k;
                yield return null;
            }
            if (debris) Destroy(debris);
            debris = null;
        }
    }
}
