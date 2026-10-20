using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KitFight.EditorTools
{
    /// <summary>Generates the 5 kits (KitData + AnimatorController) and the empty test scene.</summary>
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
            public string name, weapon, idle;
            public string[] combo;
            public float attackSpeed, moveMult;
        }

        static readonly KitSpec[] Specs =
        {
            new KitSpec { name = "Sword", weapon = "Human_Sword", idle = "Combat/1H/HumanM@CombatIdle1H01",
                combo = new[] { "Combat/1H/HumanM@Attack1H01_R", "Combat/1H/HumanM@Attack1H02_R", "Combat/1H/HumanM@Attack1H03_R" },
                attackSpeed = 1f, moveMult = 1f },
            new KitSpec { name = "Dagger", weapon = "Human_Dagger", idle = "Combat/1H/HumanM@CombatIdle1H01",
                combo = new[] { "Combat/1H/HumanM@Attack1H04_L", "Combat/1H/HumanM@Attack1H04_R", "Combat/1H/HumanM@Attack1H05_L", "Combat/1H/HumanM@Attack1H05_R" },
                attackSpeed = 1.3f, moveMult = 1.15f },
            new KitSpec { name = "Greatsword", weapon = "Human_Greatsword", idle = "Combat/2H/HumanM@CombatIdle2H01",
                combo = new[] { "Combat/2H/HumanM@Attack2H01", "Combat/2H/HumanM@Attack2H02", "Combat/2H/HumanM@Attack2H03", "Combat/2H/HumanM@Attack2H04" },
                attackSpeed = 1f, moveMult = 0.95f },
            new KitSpec { name = "Warhammer", weapon = "Human_Warhammer", idle = "Combat/2H/HumanM@CombatIdle2H01",
                combo = new[] { "Combat/2H/HumanM@Attack2H02", "Combat/2H/HumanM@Attack2H04", "Combat/2H/HumanM@Attack2H01" },
                attackSpeed = 0.8f, moveMult = 0.85f },
            new KitSpec { name = "Polearm", weapon = "Human_Polearm", idle = "Combat/Polearm/HumanM@CombatIdlePolearm01",
                combo = new[] { "Combat/Polearm/HumanM@AttackPolearm01", "Combat/Polearm/HumanM@AttackPolearm02", "Combat/Polearm/HumanM@AttackPolearm03", "Combat/Polearm/HumanM@AttackPolearm04" },
                attackSpeed = 1f, moveMult = 1f },
        };

        [MenuItem("KitFight/Build Test Scene")]
        public static void Build()
        {
            var kits = BuildKits();
            BuildScene(kits);
            Debug.Log("KitFight: test scene built -> " + ScenePath);
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

            var kits = new KitData[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                var s = Specs[i];
                var clips = new AnimationClip[s.combo.Length];
                for (int c = 0; c < clips.Length; c++) clips[c] = LoadClip(s.combo[c]);

                string ctrlPath = $"{KitsDir}/{s.name}.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
                ctrl.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

                var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = "Speed";
                tree.useAutomaticThresholds = false;
                tree.AddChild(LoadClip(s.idle), 0f);
                tree.AddChild(walk, 1f);
                tree.AddChild(run, 2f);
                tree.AddChild(sprint, 3f);

                var sm = ctrl.layers[0].stateMachine;
                sm.defaultState = ctrl.layers[0].stateMachine.states[0].state;
                for (int c = 0; c < clips.Length; c++)
                {
                    var st = sm.AddState("Attack" + c);
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
                kit.controller = ctrl;
                kit.comboClips = clips;
                kit.attackSpeed = s.attackSpeed;
                kit.moveSpeedMultiplier = s.moveMult;
                EditorUtility.SetDirty(kit);
                kits[i] = kit;
            }
            AssetDatabase.SaveAssets();
            return kits;
        }

        static void BuildScene(KitData[] kits)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            var groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ground.mat");
            if (groundMat) ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            // Player
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.9f, 0f);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
            model.name = "Model";
            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = kits[0].controller;

            player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerCombat>();
            player.AddComponent<PlayerInput>();
            player.AddComponent<KitHUD>();
            var sw = player.AddComponent<KitSwitcher>();
            sw.kits = kits;

            // Camera (the DefaultGameObjects camera)
            var cam = Camera.main;
            if (!cam) { var go = new GameObject("Main Camera") { tag = "MainCamera" }; cam = go.AddComponent<Camera>(); }
            var tp = cam.gameObject.GetComponent<ThirdPersonCamera>() ?? cam.gameObject.AddComponent<ThirdPersonCamera>();
            tp.target = player.transform;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Replace the old sample scene.
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
