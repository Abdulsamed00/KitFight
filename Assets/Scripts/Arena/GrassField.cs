using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitFight
{
    /// <summary>
    /// Visual only: scatters grass clumps over the arena floor and draws them with GPU instancing
    /// (KitFight/Grass shader). Spots blocked by colliders (props, pillars) and spike-trap plates stay bare.
    /// Every frame the fighters' positions, plus a short fading trail behind them, are sent to the shader
    /// so the blades bend away and slowly stand back up.
    /// </summary>
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        public Vector2 halfSize = new Vector2(21.5f, 21.5f);
        [Tooltip("Jittered grid spacing between clumps (m).")]
        public float spacing = 0.42f;
        public float minScale = 0.75f;
        public float maxScale = 1.3f;
        [Tooltip("No clump where a collider overlaps a sphere of this radius just above the floor.")]
        public float clearRadius = 0.3f;
        public int seed = 17;

        [Header("Trail (grass stays pressed behind a fighter for a moment)")]
        public float trailInterval = 0.15f;
        public float trailLife = 0.9f;

        const int MaxPushers = 8;   // must match _KFPushers[8] in Grass.shader
        const int Batch = 1023;

        static readonly int PushersId = Shader.PropertyToID("_KFPushers");
        static readonly int PusherCountId = Shader.PropertyToID("_KFPusherCount");
        static readonly int BladeHeightId = Shader.PropertyToID("_BladeHeight");

        readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
        readonly Vector4[] pushers = new Vector4[MaxPushers];
        readonly Dictionary<PlayerCombat, List<Vector4>> trails = new Dictionary<PlayerCombat, List<Vector4>>(); // xyz pos, w time
        MaterialPropertyBlock props;
        float nextTrailSample;

        public int Count => matrices.Count;

        void OnEnable() => Build();

        void OnDisable()
        {
            Shader.SetGlobalFloat(PusherCountId, 0f);
        }

        /// <summary>Re-scatters the clumps (called by ArenaBuilder after the props and their colliders exist).</summary>
        public void Build()
        {
            matrices.Clear();
            if (!mesh) return;

            Physics.SyncTransforms();
            var traps = FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None);
            var hits = new Collider[8];
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();

            for (float x = -halfSize.x; x <= halfSize.x; x += spacing)
            for (float z = -halfSize.y; z <= halfSize.y; z += spacing)
            {
                var p = transform.position + new Vector3(x + (R() - 0.5f) * spacing, 0f, z + (R() - 0.5f) * spacing);
                if (OnTrap(p, traps) || Blocked(p, hits)) { R(); R(); continue; }
                var rot = Quaternion.Euler(0f, R() * 360f, 0f);
                var scale = Vector3.one * Mathf.Lerp(minScale, maxScale, R());
                matrices.Add(Matrix4x4.TRS(p, rot, scale));
            }

            props ??= new MaterialPropertyBlock();
            props.SetFloat(BladeHeightId, mesh.bounds.max.y);
        }

        static bool OnTrap(Vector3 p, SpikeTrap[] traps)
        {
            foreach (var t in traps)
                if (Vector3.ProjectOnPlane(p - t.transform.position, Vector3.up).magnitude < 1.25f) return true;
            return false;
        }

        bool Blocked(Vector3 p, Collider[] hits)
        {
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * (clearRadius + 0.05f), clearRadius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!(hits[i] is CharacterController)) return true;   // fighters standing on the spawns don't count
            return false;
        }

        void Update()
        {
            if (!mesh || !material || matrices.Count == 0) return;
            UpdatePushers();

            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
                matProps = props,
                worldBounds = new Bounds(transform.position, new Vector3(halfSize.x * 2f + 2f, 4f, halfSize.y * 2f + 2f)),
            };
            for (int start = 0; start < matrices.Count; start += Batch)
                Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, Mathf.Min(Batch, matrices.Count - start), start);
        }

        void UpdatePushers()
        {
            int n = 0;
            bool sample = Time.time >= nextTrailSample;
            if (sample) nextTrailSample = Time.time + trailInterval;

            foreach (var pc in PlayerCombat.All)
            {
                var pos = pc.transform.position;
                if (n < MaxPushers) pushers[n++] = new Vector4(pos.x, pos.y, pos.z, 1f);

                if (!trails.TryGetValue(pc, out var trail)) trails[pc] = trail = new List<Vector4>();
                if (sample) trail.Insert(0, new Vector4(pos.x, pos.y, pos.z, Time.time));
                trail.RemoveAll(t => Time.time - t.w > trailLife);
                // Older samples press less, so the blades rise back up behind the fighter.
                for (int i = 0; i < trail.Count && i < 3 && n < MaxPushers; i++)
                    pushers[n++] = new Vector4(trail[i].x, trail[i].y, trail[i].z, 0.8f * (1f - (Time.time - trail[i].w) / trailLife));
            }
            Shader.SetGlobalVectorArray(PushersId, pushers);
            Shader.SetGlobalFloat(PusherCountId, n);
        }
    }
}
