using UnityEngine;
using UnityEngine.UIElements;

namespace KitFight
{
    /// <summary>
    /// UI Toolkit HUD (Assets/UI/Layouts/Hud.uxml). Read-only: polls GameManager / PlayerCombat every frame
    /// and never changes game state, so it keeps working when the match is driven by a server later.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudView : MonoBehaviour
    {
        [Tooltip("Health trail catches up at this fraction per second after a hit.")]
        public float trailSpeed = 0.6f;

        class PlayerUI
        {
            public VisualElement panel, health, trail, stamina, guard, staminaBar;
            public Label name, kit, tag, lockBadge;
            public PlayerMotor motor;
            public VisualElement[] pips;
            public VisualElement card;
            public Label cardDevice, cardKit, cardHint, cardStatus;
            public VisualElement statDamage, statRange, statSpeed, statMove;
            public Label legend;
            public float trailValue = 1f;
        }

        readonly PlayerUI[] ui = { new PlayerUI(), new PlayerUI() };
        VisualElement root, fightLayer, lobbyLayer, centerLayer;
        Label banner, countdown, subline;
        GameManager gm;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            fightLayer = root.Q("fight-layer");
            lobbyLayer = root.Q("lobby-layer");
            centerLayer = root.Q("center-layer");
            banner = root.Q<Label>("banner");
            countdown = root.Q<Label>("countdown");
            subline = root.Q<Label>("subline");

            for (int i = 0; i < 2; i++)
            {
                var u = ui[i];
                string p = "p" + (i + 1);
                u.panel = root.Q(p + "-panel");
                u.health = u.panel.Q("health-fill");
                u.trail = u.panel.Q("health-trail");
                u.staminaBar = u.panel.Q("stamina-bar");
                u.stamina = u.panel.Q("stamina-fill");
                u.guard = u.panel.Q("guard-fill");
                u.name = u.panel.Q<Label>("name");
                u.kit = u.panel.Q<Label>("kit");
                u.pips = u.panel.Query(className: "pip").ToList().ToArray();
                u.tag = root.Q<Label>(p + "-tag");
                u.lockBadge = u.panel.Q<Label>("lock");
                u.legend = root.Q<Label>(p + "-legend");

                u.card = root.Q(p + "-card");
                u.cardDevice = u.card.Q<Label>("device");
                u.cardKit = u.card.Q<Label>("kit-name");
                u.cardHint = u.card.Q<Label>("hint");
                u.cardStatus = u.card.Q<Label>("status");
                u.statDamage = u.card.Q("stat-damage");
                u.statRange = u.card.Q("stat-range");
                u.statSpeed = u.card.Q("stat-speed");
                u.statMove = u.card.Q("stat-move");
            }
        }

        void LateUpdate()
        {
            if (!gm) gm = GameManager.Instance;
            if (!gm || root == null) return;

            var phase = gm.CurrentPhase;
            bool lobby = phase == MatchPhase.Lobby;
            Show(lobbyLayer, lobby);
            Show(fightLayer, !lobby);

            for (int i = 0; i < 2; i++)
            {
                if (lobby) UpdateCard(i);
                else UpdateFighter(i, phase);
            }
            UpdateCenter(phase);
        }

        // ------------------------------------------------------------ fight

        void UpdateFighter(int i, MatchPhase phase)
        {
            var u = ui[i];
            var c = gm.Combat(i);
            var kit = gm.Kit(i);

            float hp = c.Health.Fraction;
            // The white trail drops after the red bar so the size of a hit stays readable for a moment.
            u.trailValue = hp > u.trailValue ? hp : Mathf.MoveTowards(u.trailValue, hp, trailSpeed * Time.unscaledDeltaTime);
            SetFill(u.health, hp);
            SetFill(u.trail, u.trailValue);
            u.health.EnableInClassList("low", hp < 0.3f);

            SetFill(u.stamina, c.StaminaFraction);
            u.staminaBar.EnableInClassList("ready", c.CanSpecial);
            u.staminaBar.EnableInClassList("charging", c.IsCharging);
            SetFill(u.guard, c.GuardFraction);

            u.name.text = $"OYUNCU {i + 1}";
            u.kit.text = kit ? kit.displayName.ToUpperInvariant() : "-";
            for (int w = 0; w < u.pips.Length; w++)
            {
                Show(u.pips[w], w < gm.WinsNeeded);
                u.pips[w].EnableInClassList("won", w < gm.Wins(i));
            }

            if (!u.motor) u.motor = gm.Fighter(i).GetComponent<PlayerMotor>();
            var other = ui[1 - i];
            if (!other.motor) other.motor = gm.Fighter(1 - i).GetComponent<PlayerMotor>();
            Show(u.lockBadge, u.motor.IsLockedOn);
            u.tag.EnableInClassList("targeted", other.motor.IsLockedOn);

            var src = gm.Source(i);
            u.legend.text = src != null ? src.ControlsText : "";
            Show(u.legend, phase == MatchPhase.Countdown || phase == MatchPhase.Fight);

            PlaceTag(u.tag, gm.Fighter(i).position + Vector3.up * 2.25f);
        }

        void PlaceTag(Label tag, Vector3 world)
        {
            var cam = Camera.main;
            if (!cam || tag.panel == null) { Show(tag, false); return; }
            if (cam.WorldToViewportPoint(world).z <= 0f) { Show(tag, false); return; }
            Show(tag, true);
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(tag.panel, world, cam);
            tag.style.left = p.x - tag.resolvedStyle.width * 0.5f;
            tag.style.top = p.y - tag.resolvedStyle.height;
        }

        void UpdateCenter(MatchPhase phase)
        {
            string big = "", count = "", sub = "";
            switch (phase)
            {
                case MatchPhase.Countdown:
                    big = $"ROUND {gm.Round}";
                    count = gm.PhaseTimer > 0.05f ? Mathf.CeilToInt(gm.PhaseTimer).ToString() : "";
                    break;
                case MatchPhase.Fight:
                    if (gm.ShowFightBanner) big = "DÖVÜŞ!";
                    break;
                case MatchPhase.RoundOver:
                    big = gm.Banner;
                    break;
                case MatchPhase.MatchOver:
                    big = gm.Banner;
                    sub = "Menüye dönmek için saldırı tuşuna bas";
                    break;
            }
            banner.text = big;
            countdown.text = count;
            subline.text = sub;
            Show(banner, big != "");
            Show(countdown, count != "");
            Show(subline, sub != "");
            banner.EnableInClassList("fight", phase == MatchPhase.Fight);
            Show(centerLayer, big != "" || count != "" || sub != "");
        }

        // ------------------------------------------------------------ lobby

        void UpdateCard(int i)
        {
            var u = ui[i];
            bool joined = gm.IsJoined(i), ready = gm.IsReady(i);
            u.card.EnableInClassList("joined", joined);
            u.card.EnableInClassList("ready", ready);
            Show(u.tag, false);

            if (!joined)
            {
                u.cardDevice.text = "Bekleniyor...";
                u.cardKit.text = "?";
                u.cardHint.text = i == 0
                    ? "Katılmak için saldırı tuşuna bas\nJ / Space   ·   , / Numpad1   ·   Gamepad A / RT"
                    : "Katılmak için başka bir cihazdan\nsaldırı tuşuna bas";
                u.cardStatus.text = "";
                SetStats(u, null);
                return;
            }

            var kit = gm.Kit(i);
            u.cardDevice.text = gm.Source(i).Label.ToUpperInvariant();
            u.cardKit.text = kit ? (ready ? kit.displayName : $"‹  {kit.displayName}  ›") : "-";
            u.cardHint.text = gm.Source(i).ControlsText;
            u.cardStatus.text = ready ? "HAZIR  ·  geri: blok" : "sol / sağ: kit seç   ·   saldırı: hazır   ·   blok: ayrıl";
            SetStats(u, kit);
        }

        static void SetStats(PlayerUI u, KitData kit)
        {
            // Normalised against the roster's spread so the bars compare kits at a glance.
            SetFill(u.statDamage, kit ? Mathf.InverseLerp(4f, 26f, kit.damage) : 0f);
            SetFill(u.statRange, kit ? Mathf.InverseLerp(1.2f, 3.2f, kit.range) : 0f);
            SetFill(u.statSpeed, kit ? Mathf.InverseLerp(0.8f, 1.8f, kit.attackSpeed) : 0f);
            SetFill(u.statMove, kit ? Mathf.InverseLerp(0.7f, 1.45f, kit.moveSpeedMultiplier) : 0f);
        }

        // ------------------------------------------------------------ helpers

        static void SetFill(VisualElement fill, float f) => fill.style.width = Length.Percent(Mathf.Clamp01(f) * 100f);

        static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
