using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitFight
{
    /// <summary>
    /// Visual only: hides arena wall/pillar renderers that stand between the camera and a fighter
    /// (they switch to ShadowsOnly, so the shadow stays) and shows them again once the view is clear.
    /// Sits on the camera; runs after FightCamera has moved it.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraOccluderFade : MonoBehaviour
    {
        [Tooltip("Roots (by name) whose renderers may be hidden. Built by ArenaBuilder under 'Arena'.")]
        public string[] rootNames = { "WallVisuals", "Pillars" };
        [Tooltip("Sample heights on each fighter (feet / body / head).")]
        public float[] heights = { 0.2f, 1f, 1.7f };
        [Tooltip("Bounds are grown by this much so the fighter's width counts too (CharacterController radius).")]
        public float padding = 0.3f;

        Renderer[] rends;
        Bounds[] bounds;
        ShadowCastingMode[] original;
        bool[] hidden;

        void Start()
        {
            var list = new List<Renderer>();
            foreach (var n in rootNames)
            {
                var root = GameObject.Find(n);
                if (root) list.AddRange(root.GetComponentsInChildren<Renderer>(true));
            }
            rends = list.ToArray();
            bounds = new Bounds[rends.Length];
            original = new ShadowCastingMode[rends.Length];
            hidden = new bool[rends.Length];
            for (int i = 0; i < rends.Length; i++)
            {
                // Walls and pillars never move, so their bounds are measured once.
                bounds[i] = rends[i].bounds;
                bounds[i].Expand(padding * 2f);
                original[i] = rends[i].shadowCastingMode;
            }
        }

        void LateUpdate()
        {
            if (rends == null) return;
            var cam = transform.position;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i]) continue;
                bool block = Blocks(bounds[i], cam);
                if (block == hidden[i]) continue;
                hidden[i] = block;
                rends[i].shadowCastingMode = block ? ShadowCastingMode.ShadowsOnly : original[i];
            }
        }

        bool Blocks(Bounds b, Vector3 cam)
        {
            foreach (var pc in PlayerCombat.All)
            {
                if (!pc) continue;
                var feet = pc.transform.position;
                foreach (var h in heights)
                {
                    var dir = feet + Vector3.up * h - cam;
                    float len = dir.magnitude;
                    if (len < 0.01f) continue;
                    if (b.IntersectRay(new Ray(cam, dir / len), out float d) && d < len) return true;
                }
            }
            return false;
        }

        void OnDisable()
        {
            if (rends == null) return;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] && hidden[i]) rends[i].shadowCastingMode = original[i];
                hidden[i] = false;
            }
        }
    }
}
