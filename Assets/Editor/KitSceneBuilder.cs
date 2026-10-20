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
                attackSpeed = 1f, moveMult = 1f, damage = 12f, range = 2.1f, knockback = 3f, hitStart = 0.35f },
            new KitSpec { name = "Dagger", weapon = "Human_Dagger", idle = "Combat/1H/HumanM@CombatIdle1H01",
                offhand = "Human_Dagger", offhandEuler = new Vector3(0f, 180f, 0f),
                blockLoop = "Combat/1H/HumanM@ParryDW01 - Loop", blockHit = "Combat/1H/HumanM@ParryDW01 - Hit",
                combo = new[] { "Combat/1H/HumanM@Attack1H04_L", "Combat/1H/HumanM@Attack1H04_R", "Combat/1H/HumanM@Attack1H05_L", "Combat/1H/HumanM@Attack1H05_R", "Combat/1H/HumanM@AttackDW01", "Combat/1H/HumanM@AttackDW02" },
                attackSpeed = 1.3f, moveMult = 1.15f, damage = 7f, range = 1.7f, knockback = 1.5f, hitStart = 0.35f },
            new KitSpec { name = "Greatsword", weapon = "Human_Greatsword", idle = "Combat/2H/HumanM@CombatIdle2H01",
                blockLoop = "Combat/2H/HumanM@Parry2H01 - Loop", blockHit = "Combat/2H/HumanM@Parry2H01 - Hit",
                combo = new[] { "Combat/2H/HumanM@Attack2H01", "Combat/2H/HumanM@Attack2H02", "Combat/2H/HumanM@Attack2H03", "Combat/2H/HumanM@Attack2H04" },
                attackSpeed = 1f, moveMult = 0.95f, damage = 20f, range = 2.5f, knockback = 5f, hitStart = 0.4f },
            new KitSpec { name = "Warhammer", weapon = "Human_Warhammer", idle = "Combat/2H/HumanM@CombatIdle2H01",
                blockLoop = "Combat/2H/HumanM@Parry2H01 - Loop", blockHit = "Combat/2H/HumanM@Parry2H01 - Hit",
                combo = new[] { "Combat/2H/HumanM@Attack2H02", "Combat/2H/HumanM@Attack2H04", "Combat/2H/HumanM@Attack2H01" },
                attackSpeed = 0.8f, moveMult = 0.85f, damage = 24f, range = 2.3f, knockback = 6.5f, hitStart = 0.45f },
            new KitSpec { name = "Polearm", weapon = "Human_Polearm", idle = "Combat/Polearm/HumanM@CombatIdlePolearm01",
                blockLoop = "Combat/Polearm/HumanM@ParryPolearm01 - Loop", blockHit = "Combat/Polearm/HumanM@ParryPolearm01 - Hit",
                combo = new[] { "Combat/Polearm/HumanM@AttackPolearm01", "Combat/Polearm/HumanM@AttackPolearm02", "Combat/Polearm/HumanM@AttackPolearm03", "Combat/Polearm/HumanM@AttackPolearm04" },
                attackSpeed = 1f, moveMult = 1f, damage = 14f, range = 3f, knockback = 3.5f, hitStart = 0.4f },
        };

        [MenuItem("KitFight/Build Test Scene")]
        public static void Build()
        {
            var kits = BuildKits();
            BuildScene(kits);
            Debug.Log("KitFight: scene built -> " + ScenePath);
        }

        static AnimationClip LoadClip(string relPath)
        {
            string path = Anim + relPath + ".fbx";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            Debug.LogError("Clip not found: " + path);
            return null;
        }

        static KitData[] BuildKits()
        {
            Directory.CreateDirectory(KitsDir);
            var walk = LoadClip("Movement/Walk/HumanM@Walk01_Forward");
            var run = LoadClip("Movement/Run/HumanM@Run01_Forward");
            var sprint = LoadClip("Movement/Sprint/HumanM@Sprint01_Forward");
            var hit = LoadClip("Combat/HumanM@CombatDamage01");
            var death = LoadClip("Combat/HumanM@CombatDeath01");

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
                ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
                ctrl.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

                ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = "Speed";
                tree.useAutomaticThresholds = false;
                tree.AddChild(LoadClip(s.idle), 0f);
                tree.AddChild(walk, 1f);
                tree.AddChild(run, 2f);
                tree.AddChild(sprint, 3f);

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
                sm.AddState("BlockHit").motion = blockHitClip;
                sm.AddState("Hit").motion = hit;
                sm.AddState("Death").motion = death;

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

            // One shared camera
            var cam = Camera.main;
            if (!cam) { var go = new GameObject("Main Camera") { tag = "MainCamera" }; cam = go.AddComponent<Camera>(); }
            cam.fieldOfView = 45f;
            var fc = cam.gameObject.GetComponent<FightCamera>() ?? cam.gameObject.AddComponent<FightCamera>();
            cam.transform.position = new Vector3(0f, 6f, -12f);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
