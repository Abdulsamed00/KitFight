using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KitFight.EditorTools
{
    /// <summary>
    /// Arena dressing for the test scene: wall segments + pillars along the edge, breakable props and
    /// spike traps, all under an "Arena" root. Uses the FBX models in Assets/Models/Arena when present,
    /// otherwise primitive stand-ins (re-run KitFight > Build Test Scene once the models arrive).
    /// The invisible boundary colliders are built separately by KitSceneBuilder and are kept.
    /// </summary>
    static class ArenaBuilder
    {
        const string ModelDir = "Assets/Models/Arena/";
        const string MatDir = "Assets/Materials/Arena";
        const float Half = 22f;           // playable area is +-22 m
        const float WallLen = 4f;

        enum Align { Bottom, Top }

        public static void Build(Vector3[] spawns)
        {
            var arena = new GameObject("Arena").transform;

            var stone = Mat("Arena_Stone", new Color(0.55f, 0.53f, 0.5f));
            var stoneDark = Mat("Arena_StoneDark", new Color(0.4f, 0.38f, 0.36f));
            var wood = Mat("Arena_Wood", new Color(0.55f, 0.36f, 0.2f));
            var woodDark = Mat("Arena_WoodDark", new Color(0.38f, 0.24f, 0.13f));
            var clay = Mat("Arena_Clay", new Color(0.75f, 0.45f, 0.3f));
            var metal = Mat("Arena_Metal", new Color(0.3f, 0.3f, 0.32f));
            var steel = Mat("Arena_Steel", new Color(0.72f, 0.72f, 0.75f));

            // ---------------------------------------------------------- walls + pillars
            var walls = Child(arena, "WallVisuals");
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                var rot = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < 11; i++)
                {
                    float along = -Half + WallLen * 0.5f + i * WallLen;
                    // side 0 = north (+z), then rotate around the centre for E, S, W
                    var pos = rot * new Vector3(along, 0f, Half + 0.5f);
                    var w = Place(walls, $"Wall_{side}_{i:00}", "SM_ArenaWall_01", pos, yaw, new Vector3(WallLen, 3f, 1f),
                        stone, PrimitiveType.Cube, Align.Bottom, alignLongX: true);
                    RemoveColliders(w);
                    MarkStatic(w);
                }
            }

            // Edge pillars stand just in front of the wall (back half sunk into it, front face ~1 m inside the
            // boundary) so they read as pillars and get their own collider. Corner pillars fill the gap where
            // two wall lines meet, outside the playable area.
            var pillars = Child(arena, "Pillars");
            var pillarSpots = new List<Vector2>();
            float e = Half - 0.45f;
            float[] marks = { -14f, -6f, 6f, 14f };
            foreach (var m in marks)
            {
                pillarSpots.Add(new Vector2(m, e));
                pillarSpots.Add(new Vector2(m, -e));
                pillarSpots.Add(new Vector2(e, m));
                pillarSpots.Add(new Vector2(-e, m));
            }
            float c = Half + 0.5f;
            pillarSpots.Add(new Vector2(c, c)); pillarSpots.Add(new Vector2(-c, c));
            pillarSpots.Add(new Vector2(c, -c)); pillarSpots.Add(new Vector2(-c, -c));
            int pi = 0;
            foreach (var p in pillarSpots)
            {
                var go = Place(pillars, $"Pillar_{pi++:00}", "SM_Pillar_01", new Vector3(p.x, 0f, p.y), 0f,
                    new Vector3(1.2f, 4f, 1.2f), stoneDark, PrimitiveType.Cube, Align.Bottom);
                RemoveColliders(go);
                // Capsule around the shaft, in the holder's space (so a unit-fix scale is included).
                var b = LocalBounds(go.transform, go);
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.center = b.center;
                cap.height = b.size.y;
                cap.radius = Mathf.Min(b.extents.x, b.extents.z) * 0.9f;
                // The square base pokes out past the capsule at the corners.
                var basis = go.AddComponent<BoxCollider>();
                basis.center = new Vector3(b.center.x, b.min.y + 0.25f, b.center.z);
                basis.size = new Vector3(b.size.x * 0.98f, 0.5f, b.size.z * 0.98f);
                MarkStatic(go);
            }

            // ---------------------------------------------------------- breakables
            var props = Child(arena, "Breakables");
            var layout = new (string kind, float x, float z)[]
            {
                // north-west corner group
                ("Crate", -19f, 19f), ("Crate", -17.7f, 19.2f), ("Barrel", -19.1f, 17.6f),
                // north-east corner group
                ("Barrel", 19f, 19f), ("Barrel", 17.8f, 18.7f), ("Vase", 19.2f, 17.5f),
                // south-east pair
                ("Crate", 19f, -19f), ("Vase", 17.6f, -19.2f),
                // south-west corner group
                ("Barrel", -19f, -19f), ("Crate", -17.7f, -18.8f), ("Vase", -19.2f, -17.5f),
                // mid-field, off the spawn line
                ("Crate", -12f, 3.5f), ("Barrel", 12f, -3.5f),
            };
            var rng = new System.Random(7);
            int n = 0;
            foreach (var (kind, x, z) in layout)
            {
                var pos = new Vector3(x, 0f, z);
                if (TooClose(pos, spawns, 4f)) { Debug.LogWarning($"Arena: skipped {kind} at {pos}, too close to a spawn"); continue; }
                float yaw = (float)rng.NextDouble() * 360f;
                switch (kind)
                {
                    case "Crate": MakeBreakable(props, $"Crate_{n++:00}", "SM_Crate_01", pos, yaw, new Vector3(1f, 1f, 1f), wood, PrimitiveType.Cube); break;
                    case "Barrel": MakeBreakable(props, $"Barrel_{n++:00}", "SM_Barrel_01", pos, yaw, new Vector3(0.8f, 1.1f, 0.8f), woodDark, PrimitiveType.Cylinder); break;
                    default: MakeBreakable(props, $"Vase_{n++:00}", "SM_Vase_01", pos, yaw, new Vector3(0.5f, 0.8f, 0.5f), clay, PrimitiveType.Cylinder); break;
                }
            }

            // ---------------------------------------------------------- spike traps
            var traps = Child(arena, "SpikeTraps");
            var trapSpots = new[] { new Vector3(8f, 0f, 8f), new Vector3(-8f, 0f, 8f), new Vector3(8f, 0f, -8f), new Vector3(-8f, 0f, -8f) };
            for (int i = 0; i < trapSpots.Length; i++)
                MakeSpikeTrap(traps, $"SpikeTrap_{i}", trapSpots[i], metal, steel);

            Debug.Log($"KitFight: arena built ({n} breakables, {trapSpots.Length} spike traps, models: {(HasModel("SM_ArenaWall_01") ? "FBX" : "primitive fallback")})");
        }

        // ------------------------------------------------------------------ pieces

        static void MakeBreakable(Transform parent, string name, string model, Vector3 pos, float yaw, Vector3 size, Material mat, PrimitiveType prim)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            var visual = Place(root.transform, "Visual", model, Vector3.zero, 0f, size, mat, prim, Align.Bottom);
            RemoveColliders(visual);

            // Solid collider fitted to the intact model (measured before the yaw is applied):
            // a capsule for round props (barrel, vase), a box for the crate.
            var b = LocalBounds(root.transform, visual);
            if (prim == PrimitiveType.Cylinder)
            {
                var cap = root.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.center = b.center;
                cap.height = b.size.y;
                cap.radius = b.extents.x;
            }
            else
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }

            var br = root.AddComponent<Breakable>();
            br.intactVisual = visual;
            br.brokenPrefab = LoadModel(model + "_Broken");
            br.radius = Mathf.Max(b.extents.x, b.extents.z);

            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static void MakeSpikeTrap(Transform parent, string name, Vector3 pos, Material plateMat, Material spikeMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos + Vector3.up * 0.02f; // plate top just above the ground plane

            var plate = Place(root.transform, "Base", "SM_SpikeTrap_Base_01", Vector3.zero, 0f, new Vector3(2f, 0.1f, 2f),
                plateMat, PrimitiveType.Cube, Align.Top);
            RemoveColliders(plate);

            var spikes = new GameObject("Spikes").transform;
            spikes.SetParent(root.transform, false);
            var trap = root.AddComponent<SpikeTrap>();
            trap.spikes = spikes;
            spikes.localPosition = new Vector3(0f, trap.downY, 0f);

            var fbx = LoadModel("SM_SpikeTrap_Spikes_01");
            if (fbx)
            {
                var v = Place(spikes, "Model", "SM_SpikeTrap_Spikes_01", Vector3.zero, 0f, new Vector3(1.8f, 1.2f, 1.8f), spikeMat, PrimitiveType.Cube, Align.Bottom);
                RemoveColliders(v);
            }
            else
            {
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    s.name = $"Spike_{x + 1}{z + 1}";
                    Object.DestroyImmediate(s.GetComponent<Collider>());
                    s.transform.SetParent(spikes, false);
                    s.transform.localPosition = new Vector3(x * 0.6f, 0.6f, z * 0.6f);
                    s.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
                    s.GetComponent<Renderer>().sharedMaterial = spikeMat;
                }
            }
        }

        /// <summary>
        /// Puts an FBX model (or a primitive fallback) in a "holder" child. The holder carries position,
        /// yaw, fit scale and pivot correction; the model keeps its import transform underneath.
        /// </summary>
        static GameObject Place(Transform parent, string name, string model, Vector3 pos, float yaw, Vector3 size,
            Material fallbackMat, PrimitiveType prim, Align align, bool alignLongX = false)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);

            var asset = LoadModel(model);
            if (asset)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
                FixMaterials(inst, fallbackMat);

                var b = LocalBounds(holder.transform, inst);
                if (alignLongX && b.size.z > b.size.x * 1.2f)
                {
                    inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * inst.transform.localRotation;
                    b = LocalBounds(holder.transform, inst);
                }
                // Only rescale when the import is clearly off (unit mismatch), not for small modelling differences.
                if (b.size.y > 0.0001f)
                {
                    float ratio = size.y / b.size.y;
                    if (ratio > 1.8f || ratio < 0.55f)
                    {
                        holder.transform.localScale = Vector3.one * ratio;
                        Debug.LogWarning($"Arena: {model} height {b.size.y:0.00} m, expected {size.y:0.00} m -> scaled x{ratio:0.00}");
                    }
                }
                // Pivot correction: base (or plate top) on y = 0, centred on x/z.
                // (b is in the holder's unscaled space, so the offset scales along with the holder.)
                float y = align == Align.Bottom ? b.min.y : b.max.y;
                inst.transform.localPosition -= new Vector3(b.center.x, y, b.center.z);
            }
            else
            {
                var p = GameObject.CreatePrimitive(prim);
                p.name = model + "_Placeholder";
                p.transform.SetParent(holder.transform, false);
                // Unity cylinder is 2 units tall at scale 1.
                var scale = prim == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
                p.transform.localScale = scale;
                p.transform.localPosition = new Vector3(0f, align == Align.Bottom ? size.y * 0.5f : -size.y * 0.5f, 0f);
                p.GetComponent<Renderer>().sharedMaterial = fallbackMat;
            }

            holder.transform.localPosition = pos;
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return holder;
        }

        // ------------------------------------------------------------------ helpers

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static bool TooClose(Vector3 p, Vector3[] spawns, float dist)
        {
            foreach (var s in spawns)
                if (Vector3.ProjectOnPlane(p - s, Vector3.up).magnitude < dist) return true;
            return false;
        }

        static bool HasModel(string name) => LoadModel(name) != null;

        static GameObject LoadModel(string name)
        {
            string path = ModelDir + name + ".fbx";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go) FixImporter(path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void RemoveColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        /// <summary>
        /// Static batching for never-moving pieces. No ContributeGI: lighting is realtime and the FBX have no
        /// lightmap UVs. No OccluderStatic: CameraOccluderFade hides these at runtime.
        /// </summary>
        static void MarkStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Bounds of all renderers under 'target', in 'space' local coordinates.</summary>
        static Bounds LocalBounds(Transform space, GameObject target)
        {
            var rends = target.GetComponentsInChildren<Renderer>(true);
            bool first = true;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in rends)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = space.InverseTransformPoint(corner);
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; }
                    else b.Encapsulate(lp);
                }
            }
            return b;
        }

        // ------------------------------------------------------------------ materials / import

        static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(LitShader());
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != LitShader()) m.shader = LitShader();
            SetColor(m, color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        }

        static bool IsBadShader(Material m) =>
            !m || !m.shader || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader"
            || !m.shader.name.StartsWith("Universal Render Pipeline");

        /// <summary>
        /// Static props: no animation/rig. If the FBX's embedded materials would render pink under URP,
        /// remap each one to a URP Lit material (same colour) in Assets/Materials/Arena.
        /// </summary>
        static void FixImporter(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!imp) return;
            bool changed = false;
            if (imp.animationType != ModelImporterAnimationType.None) { imp.animationType = ModelImporterAnimationType.None; changed = true; }
            if (imp.importAnimation) { imp.importAnimation = false; changed = true; }
            if (imp.importCameras) { imp.importCameras = false; changed = true; }
            if (imp.importLights) { imp.importLights = false; changed = true; }

            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is Material m) || !IsBadShader(m)) continue;
                var color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.gray;
                var safe = Mat("FBX_" + Sanitize(m.name), color);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), safe);
                changed = true;
                Debug.LogWarning($"Arena: {path} material '{m.name}' uses '{(m.shader ? m.shader.name : "none")}', remapped to URP Lit");
            }
            if (changed) imp.SaveAndReimport();
        }

        static string Sanitize(string s)
        {
            foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s;
        }

        /// <summary>Last line of defence for scene instances: swap any material that still would not render.</summary>
        static void FixMaterials(GameObject inst, Material fallback)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool bad = false;
                for (int i = 0; i < mats.Length; i++)
                    if (IsBadShader(mats[i])) { mats[i] = fallback; bad = true; }
                if (bad) r.sharedMaterials = mats;
            }
        }
    }
}
