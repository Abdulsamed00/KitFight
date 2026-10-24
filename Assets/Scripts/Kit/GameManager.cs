using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KitFight
{
    /// <summary>
    /// Match flow for two local players on one screen: Lobby (join + kit select) -> Countdown -> Fight
    /// -> RoundOver -> ... -> MatchOver. Also draws a throwaway IMGUI HUD.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public PlayerInput[] players = new PlayerInput[2];
        public Vector3[] spawnPositions = { new Vector3(-3f, 0f, 0f), new Vector3(3f, 0f, 0f) };
        public Color[] colors = { new Color(0.35f, 0.6f, 1f), new Color(1f, 0.4f, 0.35f) };
        public int winsNeeded = 2;
        public float countdownTime = 3f;
        public float roundOverTime = 3f;

        enum Phase { Lobby, Countdown, Fight, RoundOver, MatchOver }

        class Slot
        {
            public PlayerInput input;
            public KitSwitcher kit;
            public PlayerCombat combat;
            public PlayerMotor motor;
            public InputSource src;
            public bool ready;
            public int wins;
        }

        readonly Slot[] slots = new Slot[2];
        readonly List<InputSource> sources = new List<InputSource>();
        readonly Dictionary<Gamepad, InputSource> padSources = new Dictionary<Gamepad, InputSource>();

        /// <summary>True only while a round is being fought (arena hazards run only then).</summary>
        public static bool IsFighting => instance && instance.phase == Phase.Fight;
        static GameManager instance;

        Phase phase = Phase.Lobby;
        float timer;
        int round = 1;
        string banner = "";
        float fightBannerUntil;

        void OnDestroy() { if (instance == this) instance = null; }

        void Awake()
        {
            instance = this;
            sources.Add(new InputSource(InputSource.Kind.Keyboard1));
            sources.Add(new InputSource(InputSource.Kind.Keyboard2));

            for (int i = 0; i < 2; i++)
            {
                var p = players[i];
                slots[i] = new Slot
                {
                    input = p,
                    kit = p.GetComponent<KitSwitcher>(),
                    combat = p.GetComponent<PlayerCombat>(),
                    motor = p.GetComponent<PlayerMotor>(),
                };
            }
            var cam = FindFirstObjectByType<FightCamera>();
            if (cam) { cam.p1 = slots[0].motor.transform; cam.p2 = slots[1].motor.transform; }
            slots[0].motor.faceTarget = slots[1].motor.transform;
            slots[1].motor.faceTarget = slots[0].motor.transform;
        }

        void Start() => EnterLobby();

        // ------------------------------------------------------------ flow

        void EnterLobby()
        {
            phase = Phase.Lobby;
            round = 1;
            banner = "";
            foreach (var s in slots) { s.ready = false; s.wins = 0; }
            ResetFighters(false);
        }

        void ResetFighters(bool enableControl)
        {
            for (int i = 0; i < 2; i++)
            {
                var s = slots[i];
                s.combat.ResetFighter();
                var dir = (spawnPositions[1 - i] - spawnPositions[i]).normalized;
                s.motor.Teleport(spawnPositions[i], Quaternion.LookRotation(dir));
                s.input.controlEnabled = enableControl;
            }
            foreach (var b in Breakable.All) b.ResetObject();
        }

        void StartRound()
        {
            ResetFighters(false);
            phase = Phase.Countdown;
            timer = countdownTime;
        }

        void Update()
        {
            RefreshPads();
            foreach (var src in sources) src.Poll();

            switch (phase)
            {
                case Phase.Lobby: UpdateLobby(); break;
                case Phase.Countdown: UpdateCountdown(); break;
                case Phase.Fight: UpdateFight(); break;
                case Phase.RoundOver: UpdateRoundOver(); break;
                case Phase.MatchOver: UpdateMatchOver(); break;
            }
        }

        void RefreshPads()
        {
            foreach (var pad in Gamepad.all)
            {
                if (padSources.ContainsKey(pad)) continue;
                var s = new InputSource(InputSource.Kind.Gamepad, pad);
                padSources[pad] = s;
                sources.Add(s);
            }
        }

        bool IsTaken(InputSource s) => slots[0].src == s || slots[1].src == s;

        void UpdateLobby()
        {
            for (int i = 0; i < 2; i++)
            {
                var s = slots[i];
                if (s.src == null)
                {
                    foreach (var c in sources)
                    {
                        if (IsTaken(c) || !c.AttackDown) continue;
                        s.src = c;
                        s.input.source = c;
                        break;
                    }
                    continue; // the joining press is not also a confirm
                }

                if (!s.ready)
                {
                    if (s.src.NavRight) s.kit.Cycle(1);
                    if (s.src.NavLeft) s.kit.Cycle(-1);
                    if (s.src.AttackDown) s.ready = true;
                    else if (s.src.BlockDown) { s.src = null; s.input.source = null; }
                }
                else if (s.src.BlockDown) s.ready = false;
            }

            if (slots[0].ready && slots[1].ready) StartRound();
        }

        void UpdateCountdown()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            phase = Phase.Fight;
            fightBannerUntil = Time.time + 0.8f;
            foreach (var s in slots) s.input.controlEnabled = true;
        }

        void UpdateFight()
        {
            bool d0 = slots[0].combat.IsDead, d1 = slots[1].combat.IsDead;
            if (!d0 && !d1) return;

            foreach (var s in slots) s.input.controlEnabled = false;
            phase = Phase.RoundOver;
            timer = roundOverTime;

            if (d0 && d1) { banner = "BERABERE"; return; }
            int winner = d0 ? 1 : 0;
            slots[winner].wins++;
            slots[winner].combat.Celebrate();
            banner = $"OYUNCU {winner + 1} ROUNDU KAZANDI";
        }

        void UpdateRoundOver()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            for (int i = 0; i < 2; i++)
            {
                if (slots[i].wins < winsNeeded) continue;
                phase = Phase.MatchOver;
                banner = $"OYUNCU {i + 1} MAÇI KAZANDI!";
                return;
            }
            round++;
            StartRound();
        }

        void UpdateMatchOver()
        {
            foreach (var s in slots)
                if (s.src != null && s.src.AttackDown) { EnterLobby(); return; }
        }

        // ------------------------------------------------------------ HUD

        GUIStyle label, big, small, center;

        void InitStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            big = new GUIStyle(label) { fontSize = 84, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            center = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
        }

        static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color c)
        {
            var old = GUI.contentColor;
            GUI.contentColor = Color.black;
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            GUI.contentColor = c;
            GUI.Label(r, text, style);
            GUI.contentColor = old;
        }

        void OnGUI()
        {
            InitStyles();
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float W = Screen.width / s;

            DrawFighterLabels();
            for (int i = 0; i < 2; i++) DrawBar(i, W);
            if (phase == Phase.Countdown || phase == Phase.Fight) DrawLegend(W);

            switch (phase)
            {
                case Phase.Lobby:
                    Shadowed(new Rect(0, 40, W, 60), "KIT SEÇİMİ", new GUIStyle(big) { fontSize = 48 }, Color.white);
                    for (int i = 0; i < 2; i++) DrawLobbyPanel(i, W);
                    break;
                case Phase.Countdown:
                    Shadowed(new Rect(0, 360, W, 140), $"Round {round}", big, Color.white);
                    Shadowed(new Rect(0, 470, W, 140), timer > 0.05f ? Mathf.CeilToInt(timer).ToString() : "", big, Color.yellow);
                    break;
                case Phase.Fight:
                    if (Time.time < fightBannerUntil)
                        Shadowed(new Rect(0, 400, W, 140), "DÖVÜŞ!", big, Color.red);
                    break;
                case Phase.RoundOver:
                    Shadowed(new Rect(0, 380, W, 140), banner, new GUIStyle(big) { fontSize = 64 }, Color.yellow);
                    break;
                case Phase.MatchOver:
                    Shadowed(new Rect(0, 360, W, 140), banner, new GUIStyle(big) { fontSize = 64 }, Color.yellow);
                    Shadowed(new Rect(0, 470, W, 60), "Menüye dönmek için saldırı tuşuna bas", center, Color.white);
                    break;
            }
        }

        void DrawBar(int i, float W)
        {
            var s = slots[i];
            const float bw = 640f, bh = 36f, margin = 50f, y = 40f;
            float x = i == 0 ? margin : W - margin - bw;
            Box(new Rect(x - 4, y - 4, bw + 8, bh + 8), new Color(0, 0, 0, 0.7f));
            float f = s.combat.Health.Fraction;
            float fw = bw * f;
            float fx = i == 0 ? x : x + bw - fw; // P2 drains toward the edge
            Box(new Rect(fx, y, fw, bh), colors[i]);

            // guard meter (fills while blocking; full = guard break)
            float g = s.combat.GuardFraction;
            if (g > 0.01f)
            {
                float gw = bw * g;
                Box(new Rect(i == 0 ? x : x + bw - gw, y + bh + 2, gw, 6), Color.Lerp(Color.white, new Color(1f, 0.5f, 0f), g));
            }

            string kitName = s.kit.Current != null ? s.kit.Current.displayName : "-";
            var a = i == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            Shadowed(new Rect(x, y + bh + 6, bw, 36), $"OYUNCU {i + 1} - {kitName}", new GUIStyle(label) { alignment = a, fontSize = 22 }, colors[i]);

            // win pips
            for (int w = 0; w < winsNeeded; w++)
            {
                float px = i == 0 ? x + w * 34 : x + bw - 28 - w * 34;
                Box(new Rect(px, y + bh + 48, 24, 24), w < s.wins ? Color.yellow : new Color(1, 1, 1, 0.25f));
            }
        }

        void DrawLegend(float W)
        {
            const float lw = 700f;
            for (int i = 0; i < 2; i++)
            {
                if (slots[i].src == null) continue;
                var r = new Rect(i == 0 ? 50f : W - 50f - lw, 1030f, lw, 34f);
                var st = new GUIStyle(small) { alignment = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, fontSize = 17 };
                Shadowed(r, slots[i].src.ControlsText, st, new Color(1f, 1f, 1f, 0.75f));
            }
        }

        void DrawFighterLabels()
        {
            var cam = Camera.main;
            if (!cam) return;
            for (int i = 0; i < 2; i++)
            {
                var wp = slots[i].motor.transform.position + Vector3.up * 2.2f;
                var sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0f) continue;
                float s = Screen.height / 1080f;
                float x = sp.x / s, y = (Screen.height - sp.y) / s;
                Shadowed(new Rect(x - 40, y - 20, 80, 40), $"P{i + 1}", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter }, colors[i]);
            }
        }

        void DrawLobbyPanel(int i, float W)
        {
            var s = slots[i];
            const float pw = 640f, ph = 300f;
            float x = i == 0 ? 50f : W - 50f - pw;
            var r = new Rect(x, 700, pw, ph);
            Box(r, new Color(0, 0, 0, 0.6f));
            Box(new Rect(r.x, r.y, pw, 6), colors[i]);

            var head = new Rect(r.x + 20, r.y + 16, pw - 40, 36);
            Shadowed(head, $"OYUNCU {i + 1}", label, colors[i]);

            if (s.src == null)
            {
                string hint = i == 0
                    ? "Katılmak için saldırı tuşuna bas:\nJ / Space  |  , / Numpad1  |  Gamepad A / X / RT"
                    : "Katılmak için saldırı tuşuna bas:\n(farklı bir cihazdan)";
                GUI.Label(new Rect(r.x + 20, r.y + 70, pw - 40, 160), hint, small);
                return;
            }

            var kit = s.kit.Current;
            GUI.Label(new Rect(r.x + 20, r.y + 54, pw - 40, 28), $"{s.src.Label}  -  {s.src.ControlsText}", small);

            var kitStyle = new GUIStyle(big) { fontSize = 56 };
            Shadowed(new Rect(r.x, r.y + 96, pw, 70), s.ready ? kit.displayName : $"<  {kit.displayName}  >", kitStyle, Color.white);
            if (kit != null)
                GUI.Label(new Rect(r.x + 20, r.y + 176, pw - 40, 30),
                    $"Hasar {kit.damage:0}   Menzil {kit.range:0.0}   Saldırı hızı {kit.attackSpeed:0.0}x   Hareket {kit.moveSpeedMultiplier:0.00}x", center);

            string bottom = s.ready ? "HAZIR!  (Geri: blok tuşu)" : "Sol/Sağ: kit seç   Saldırı: onayla   Blok: ayrıl";
            Shadowed(new Rect(r.x, r.y + 236, pw, 40), bottom, center, s.ready ? Color.green : Color.white);
        }
    }
}
