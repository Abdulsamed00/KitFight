using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KitFight
{
    /// <summary>
    /// Match flow for two local players on one screen: Lobby (join + kit select) -> Countdown -> Fight
    /// -> RoundOver -> ... -> MatchOver. The HUD (HudView) only reads the state exposed at the bottom.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public PlayerInput[] players = new PlayerInput[2];
        public Vector3[] spawnPositions = { new Vector3(-3f, 0f, 0f), new Vector3(3f, 0f, 0f) };
        public Color[] colors = { new Color(0.35f, 0.6f, 1f), new Color(1f, 0.4f, 0.35f) };
        public int winsNeeded = 2;
        public float countdownTime = 3f;
        public float roundOverTime = 3f;

        enum Phase { Lobby, Countdown, Fight, RoundOver, MatchOver } // same order as MatchPhase

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
            Pickup.ClearAll();
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

        // ------------------------------------------------------------ read-only state for the HUD

        public static GameManager Instance => instance;
        public MatchPhase CurrentPhase => (MatchPhase)phase;
        public float PhaseTimer => timer;
        public int Round => round;
        public string Banner => banner;
        public bool ShowFightBanner => phase == Phase.Fight && Time.time < fightBannerUntil;
        public int WinsNeeded => winsNeeded;

        public bool IsJoined(int i) => slots[i].src != null;
        public bool IsReady(int i) => slots[i].ready;
        public int Wins(int i) => slots[i].wins;
        public InputSource Source(int i) => slots[i].src;
        public KitData Kit(int i) => slots[i].kit.Current;
        public PlayerCombat Combat(int i) => slots[i].combat;
        public Transform Fighter(int i) => slots[i].motor.transform;
    }

    public enum MatchPhase { Lobby, Countdown, Fight, RoundOver, MatchOver }
}
