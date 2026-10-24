using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitFight
{
    /// <summary>
    /// Visual only: scatters grass clumps over the arena floor and draws them with GPU instancing
    /// (KitFight/Grass shader). Spots blocked by colliders (props, pillars) and spike-trap plates stay bare.
    /// Every frame the fighters' positions, plus a short fading trail behind them, are sent to the shader
    /// so the blades lean away and rustle, harder the faster the fighter moves, and calm down behind them.
    /// </summary>
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        public Vector2 halfSize = new Vector2(21.5f, 21.5f);
        [Tooltip("Jittered grid spacing between clumps (m).")]
        public float spacing = 0.42f;
        public float minScale = 0.6f;
        public float maxScale = 1.05f;
        [Tooltip("No clump where a collider overlaps a sphere of this radius just above the floor.")]
        public float clearRadius = 0.3f;
        public int seed = 17;

        [Header("Trail (grass keeps rustling behind a fighter for a moment)")]
        public float trailInterval = 0.15f;
        public float trailLife = 0.9f;
        [Tooltip("Push strength while standing still (0..1); full strength at runSpeed.")]
        public float idleStrength = 0.15f;
        public float runSpeed = 4.5f;

        const int MaxPushers = 8;   // must match _KFPushers[8] in Grass.shader
        const int Batch = 1023;

        static readonly int PushersId = Shader.PropertyToID("_KFPushers");
        static readonly int PusherCountId = Shader.PropertyToID("_KFPusherCount");
        static readonly int BladeHeightId = Shader.PropertyToID("_BladeHeight");

        readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
        readonly Vector4[] pushers = new Vector4[MaxPushers];
        struct TrailPoint { public Vector3 pos; public float time, strength; }

        class Mover
        {
            public Vector3 lastPos;
            public float strength;
            public readonly List<TrailPoint> trail = new List<TrailPoint>();
        }

        readonly Dictionary<PlayerCombat, Mover> movers = new Dictionary<PlayerCombat, Mover>();
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
                if (!movers.TryGetValue(pc, out var m)) movers[pc] = m = new Mover { lastPos = pos };

                // Strength follows the fighter's ground speed (smoothed), so standing still barely stirs the grass.
                float speed = Time.deltaTime > 0f ? Vector3.ProjectOnPlane(pos - m.lastPos, Vector3.up).magnitude / Time.deltaTime : 0f;
                m.lastPos = pos;
                float target = Mathf.Lerp(idleStrength, 1f, Mathf.Clamp01(speed / runSpeed));
                m.strength = Mathf.MoveTowards(m.strength, target, 4f * Time.deltaTime);
                if (n < MaxPushers) pushers[n++] = new Vector4(pos.x, pos.y, pos.z, m.strength);

                if (sample) m.trail.Insert(0, new TrailPoint { pos = pos, time = Time.time, strength = m.strength });
                m.trail.RemoveAll(t => Time.time - t.time > trailLife);
                // Older samples rustle less, so the grass calms down behind the fighter.
                for (int i = 0; i < m.trail.Count && i < 3 && n < MaxPushers; i++)
                {
                    var t = m.trail[i];
                    pushers[n++] = new Vector4(t.pos.x, t.pos.y, t.pos.z, t.strength * 0.8f * (1f - (Time.time - t.time) / trailLife));
                }
            }
            Shader.SetGlobalVectorArray(PushersId, pushers);
            Shader.SetGlobalFloat(PusherCountId, n);
        }
    }
}
