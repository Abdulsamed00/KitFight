using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KitFight.EditorTools
{
    /// <summary>Generates the 5 kits (KitData + AnimatorController) and the two-player arena scene.</summary>
    public static class KitSceneBuilder
    {
        const string Root = "Assets/Kevin Iglesias/Human Animations/";
        const string Anim = Root + "Animations/Male/";
        const string Weapons = Root + "Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/";
        const string ModelPath = Root + "Models/HumanM_Model.fbx";
        // Swap this path (relative to Animations/Male/, no extension) to use another dodge clip, e.g. a Mixamo one.
        const string DodgeClipPath = "Combat/HumanM@Dodge01";
        const string UpperMaskPath = Root + "Models/Avatar Masks/Human Body Upper Mask.mask";
        const string KitsDir = "Assets/Kits";
        const string ScenePath = "Assets/Scenes/KitTest.unity";

        struct KitSpec
        {
            public string name, weapon, idle, offhand, blockLoop, blockHit;
            public Vector3 offhandEuler;
            public string[] combo;
            public float attackSpeed, moveMult;
            public float damage, range, knockback, hitStart;
        }

        static readonly KitSpec[] Specs =
        {
            new KitSpec { name = "Sword", weapon = "Human_Sword", idle = "Combat/1H/HumanM@CombatIdle1H01",
                offhand = "Human_Shield",
                blockLoop = "Combat/Shield/HumanM@BlockShield01 - Loop", blockHit = "Combat/Shield/HumanM@BlockShield01 - Hit",
                combo = new[] { "Combat/1H/HumanM@Attack1H01_R", "Combat/1H/HumanM@Attack1H02_R", "Combat/1H/HumanM@Attack1H03_R" },
                attackSpeed = 1.3f, moveMult = 1f, damage = 12f, range = 2.1f, knockback = 3f, hitStart = 0.35f },
            new KitSpec { name = "Dagger", weapon = "Human_Dagger", idle = "Combat/1H/HumanM@CombatIdle1H01",
                offhand = "Human_Dagger", offhandEuler = new Vector3(0f, 180f, 0f),
                blockLoop = "Combat/1H/HumanM@ParryDW01 - Loop", blockHit = "Combat/1H/HumanM@ParryDW01 - Hit",
                combo = new[] { "Combat/1H/HumanM@Attack1H04_L", "Combat/1H/HumanM@Attack1H04_R", "Combat/1H/HumanM@Attack1H05_L", "Combat/1H/HumanM@Attack1H05_R", "Combat/1H/HumanM@AttackDW01", "Combat/1H/HumanM@AttackDW02" },
                attackSpeed = 1.6f, moveMult = 1.15f, damage = 7f, range = 1.7f, knockback = 1.5f, hitStart = 0.35f },
            new KitSpec { name = "Greatsword", weapon = "Human_Greatsword", idle = "Combat/2H/HumanM@CombatIdle2H01",
                blockLoop = "Combat/2H/HumanM@Parry2H01 - Loop", blockHit = "Combat/2H/HumanM@Parry2H01 - Hit",
                combo = new[] { "Combat/2H/HumanM@Attack2H01", "Combat/2H/HumanM@Attack2H02", "Combat/2H/HumanM@Attack2H03", "Combat/2H/HumanM@Attack2H04" },
                attackSpeed = 1.25f, moveMult = 0.95f, damage = 20f, range = 2.5f, knockback = 5f, hitStart = 0.4f },
            new KitSpec { name = "Warhammer", weapon = "Human_Warhammer", idle = "Combat/2H/HumanM@CombatIdle2H01",
                blockLoop = "Combat/2H/HumanM@Parry2H01 - Loop", blockHit = "Combat/2H/HumanM@Parry2H01 - Hit",
                combo = new[] { "Combat/2H/HumanM@Attack2H02", "Combat/2H/HumanM@Attack2H04", "Combat/2H/HumanM@Attack2H01" },
                attackSpeed = 1.05f, moveMult = 0.85f, damage = 24f, range = 2.3f, knockback = 6.5f, hitStart = 0.45f },
            new KitSpec { name = "Polearm", weapon = "Human_Polearm", idle = "Combat/Polearm/HumanM@CombatIdlePolearm01",
                blockLoop = "Combat/Polearm/HumanM@ParryPolearm01 - Loop", blockHit = "Combat/Polearm/HumanM@ParryPolearm01 - Hit",
                combo = new[] { "Combat/Polearm/HumanM@AttackPolearm01", "Combat/Polearm/HumanM@AttackPolearm02", "Combat/Polearm/HumanM@AttackPolearm03", "Combat/Polearm/HumanM@AttackPolearm04" },
                attackSpeed = 1.3f, moveMult = 1f, damage = 14f, range = 3f, knockback = 3.5f, hitStart = 0.4f },
        };

        [MenuItem("KitFight/Build Test Scene")]
        public static void Build()
        {
            var kits = BuildKits();
            BuildScene(kits);
            Debug.Log("KitFight: scene built -> " + ScenePath);
        }

        static void AddState(AnimatorStateMachine sm, string name, Motion motion, float speed)
        {
            var st = sm.AddState(name);
            st.motion = motion;
            st.speed = speed;
        }

        static void AddDirections(BlendTree tree, string prefix, float radius, bool sprint)
        {
            const float d = 0.7071f;
            var dirs = sprint
                ? new (string, Vector2)[] { ("Forward", new Vector2(0, 1)), ("ForwardLeft", new Vector2(-d, d)), ("ForwardRight", new Vector2(d, d)), ("Left", new Vector2(-1, 0)), ("Right", new Vector2(1, 0)) }
                : new (string, Vector2)[] { ("Forward", new Vector2(0, 1)), ("Backward", new Vector2(0, -1)), ("Left", new Vector2(-1, 0)), ("Right", new Vector2(1, 0)),
                                            ("ForwardLeft", new Vector2(-d, d)), ("ForwardRight", new Vector2(d, d)), ("BackwardLeft", new Vector2(-d, -d)), ("BackwardRight", new Vector2(d, -d)) };
            foreach (var (name, dir) in dirs) tree.AddChild(LoadClip(prefix + name), dir * radius);
        }

        static AnimationClip LoadClip(string relPath) => LoadClipAt(Anim + relPath + ".fbx");

        static AnimationClip LoadClipAt(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            Debug.LogError("Clip not found: " + path);
            return null;
        }

        static KitData[] BuildKits()
        {
            Directory.CreateDirectory(KitsDir);
            var upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
            var hit = LoadClip("Combat/HumanM@CombatDamage01");
            var death = LoadClip("Combat/HumanM@CombatDeath01");
            var dodge = LoadClip(DodgeClipPath);
            var dodgeBack = LoadClipAt("Assets/Mixamo/Mixamo@DodgeBack.fbx");
            var dodgeRight = LoadClipAt("Assets/Mixamo/Mixamo@DodgeRight.fbx");
            var hitHeavy = LoadClipAt("Assets/Mixamo/Mixamo@HitHeavy.fbx");
            var blockImpact = LoadClipAt("Assets/Mixamo/Mixamo@BlockImpact.fbx");
            var stunned = LoadClipAt("Assets/Mixamo/Mixamo@Stunned.fbx");
            var knockedDown = LoadClipAt("Assets/Mixamo/Mixamo@KnockedDown.fbx");
            var gettingUp = LoadClipAt("Assets/Mixamo/Mixamo@GettingUp.fbx");
            var victory = LoadClipAt("Assets/Mixamo/Mixamo@Victory.fbx");

            var kits = new KitData[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                var s = Specs[i];
                var clips = new AnimationClip[s.combo.Length];
                for (int c = 0; c < clips.Length; c++) clips[c] = LoadClip(s.combo[c]);
                var blockClip = LoadClip(s.blockLoop);
                var blockHitClip = LoadClip(s.blockHit);

                string ctrlPath = $"{KitsDir}/{s.name}.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
                ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
                ctrl.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

                // 8-direction locomotion: (MoveX, MoveZ) = local move direction * gait (1 walk, 2 run, 3 sprint).
                // Facing the opponent while moving sideways/back (attacking, blocking) then strafes properly.
                ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
                tree.blendType = BlendTreeType.FreeformDirectional2D;
                tree.blendParameter = "MoveX";
                tree.blendParameterY = "MoveZ";
                tree.AddChild(LoadClip(s.idle), Vector2.zero);
                AddDirections(tree, "Movement/Walk/HumanM@Walk01_", 1f, false);
                AddDirections(tree, "Movement/Run/HumanM@Run01_", 2f, false);
                AddDirections(tree, "Movement/Sprint/HumanM@Sprint01_", 3f, true);

                var sm = ctrl.layers[0].stateMachine;
                sm.defaultState = sm.states[0].state;
                for (int c = 0; c < clips.Length; c++)
                {
                    var st = sm.AddState("Attack" + c);
                    st.motion = clips[c];
                    st.speedParameterActive = true;
                    st.speedParameter = "AttackSpeed";
                }
                sm.AddState("Block").motion = blockClip;
                // Reaction speeds are matched to the timers in PlayerCombat; change both together.
                AddState(sm, "BlockHit", blockHitClip, 1.3f);
                AddState(sm, "Hit", hit, 1.3f);
                sm.AddState("Death").motion = death;
                AddState(sm, "Dodge", dodge, 2.2f); // forward (Kevin Iglesias)
                AddState(sm, "DodgeBack", dodgeBack, 4.6f);
                AddState(sm, "DodgeRight", dodgeRight, 3.1f);
                var left = sm.AddState("DodgeLeft"); left.motion = dodgeRight; left.speed = 3.1f; left.mirror = true;
                AddState(sm, "HitHeavy", hitHeavy, 1.9f);
                AddState(sm, "BlockImpact", blockImpact, 1.6f);
                AddState(sm, "Stunned", stunned, 2f);
                AddState(sm, "KnockedDown", knockedDown, 2.2f);
                AddState(sm, "GettingUp", gettingUp, 2.4f);
                AddState(sm, "Victory", victory, 1f);

                // Upper-body layer: attacks play here so the legs can keep walking/running while swinging.
                // PlayerCombat drives its weight (1 while attacking) and also plays the attack full-body when standing still.
                ctrl.AddLayer("UpperBody");
                var layers = ctrl.layers;
                layers[1].avatarMask = upperMask;
                layers[1].defaultWeight = 0f;
                layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
                ctrl.layers = layers;
                var usm = ctrl.layers[1].stateMachine;
                usm.defaultState = usm.AddState("Empty");
                for (int c = 0; c < clips.Length; c++)
                {
                    var st = usm.AddState("Attack" + c);
                    st.motion = clips[c];
                    st.speedParameterActive = true;
                    st.speedParameter = "AttackSpeed";
                }

                string kitPath = $"{KitsDir}/Kit_{s.name}.asset";
                var kit = AssetDatabase.LoadAssetAtPath<KitData>(kitPath);
                if (!kit)
                {
                    kit = ScriptableObject.CreateInstance<KitData>();
                    AssetDatabase.CreateAsset(kit, kitPath);
                }
                kit.displayName = s.name;
                kit.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons + s.weapon + ".prefab");
                kit.offhandPrefab = string.IsNullOrEmpty(s.offhand) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(Weapons + s.offhand + ".prefab");
                kit.offhandEuler = s.offhandEuler;
                kit.controller = ctrl;
                kit.comboClips = clips;
                kit.blockClip = blockClip;
                kit.blockHitClip = blockHitClip;
                kit.damage = s.damage;
                kit.range = s.range;
                kit.knockback = s.knockback;
                kit.hitStart = s.hitStart;
                kit.attackSpeed = s.attackSpeed;
                kit.moveSpeedMultiplier = s.moveMult;
                EditorUtility.SetDirty(kit);
                kits[i] = kit;
            }
            AssetDatabase.SaveAssets();
            return kits;
        }

        static GameObject CreatePlayer(string name, KitData[] kits, int startKit, Color color, Vector3 pos)
        {
            var player = new GameObject(name);
            player.transform.position = pos;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.9f, 0f);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
            model.name = "Model";
            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = kits[startKit].controller;

            player.AddComponent<Health>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerCombat>();
            player.AddComponent<PlayerInput>();
            player.AddComponent<PlayerTint>().color = color;
            var sw = player.AddComponent<KitSwitcher>();
            sw.kits = kits;
            sw.startIndex = startKit;
            return player;
        }

        static void AddWall(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var w = new GameObject(name);
            w.transform.SetParent(parent);
            w.transform.position = pos;
            w.AddComponent<BoxCollider>().size = size;
        }

        static void BuildScene(KitData[] kits)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Arena: flat ground + invisible walls
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60 x 60
            var groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ground.mat");
            if (groundMat) ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var walls = new GameObject("Walls").transform;
            const float half = 22f, h = 10f, t = 2f;
            AddWall(walls, "N", new Vector3(0, h / 2, half + t / 2), new Vector3(2 * half, h, t));
            AddWall(walls, "S", new Vector3(0, h / 2, -half - t / 2), new Vector3(2 * half, h, t));
            AddWall(walls, "E", new Vector3(half + t / 2, h / 2, 0), new Vector3(t, h, 2 * half));
            AddWall(walls, "W", new Vector3(-half - t / 2, h / 2, 0), new Vector3(t, h, 2 * half));

            // Players
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            var p1 = CreatePlayer("Player1", kits, 0, gm.colors[0], gm.spawnPositions[0]);
            var p2 = CreatePlayer("Player2", kits, 1, gm.colors[1], gm.spawnPositions[1]);
            gm.players = new[] { p1.GetComponent<PlayerInput>(), p2.GetComponent<PlayerInput>() };

            // Arena dressing (wall/pillar visuals, breakables, spike traps); the invisible walls above stay.
            ArenaBuilder.Build(gm.spawnPositions);

            // One shared camera
            var cam = Camera.main;
            if (!cam) { var go = new GameObject("Main Camera") { tag = "MainCamera" }; cam = go.AddComponent<Camera>(); }
            cam.fieldOfView = 45f;
            var fc = cam.gameObject.GetComponent<FightCamera>() ?? cam.gameObject.AddComponent<FightCamera>();
            if (!cam.GetComponent<CameraOccluderFade>()) cam.gameObject.AddComponent<CameraOccluderFade>();
            cam.transform.position = new Vector3(0f, 6f, -12f);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
