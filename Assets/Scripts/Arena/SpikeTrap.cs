using System.Collections.Generic;
using UnityEngine;

namespace KitFight
{
    /// <summary>
    /// Floor trap: Idle -> Warning (spikes peek out and shake) -> Strike (spikes shoot up, every player
    /// on the plate takes one unblockable hit) -> Hold -> Retract -> Idle. Only runs during the Fight phase.
    /// </summary>
    public class SpikeTrap : MonoBehaviour
    {
        enum State { Idle, Warning, Strike, Hold, Retract }

        [Tooltip("Moving spike bundle (child). Its local Y is animated; 0 = fully up.")]
        public Transform spikes;
        public float damage = 10f;
        public float knockback = 2f;
        [Tooltip("Half size of the damaging square (plate is 2x2 m).")]
        public float halfSize = 1f;

        public float idleMin = 3f, idleMax = 5f;
        public float warningTime = 0.6f, strikeTime = 0.1f, holdTime = 0.6f, retractTime = 0.4f;
        [Tooltip("Spike Y when hidden (spikes are 1.2 m tall).")]
        public float downY = -1.25f;
        [Tooltip("How far the tips peek out during the warning.")]
        public float warningRise = 0.15f;

        State state;
        float timer;
        readonly HashSet<PlayerCombat> hitThisStrike = new HashSet<PlayerCombat>();

        public bool IsDangerous => state == State.Strike;

        void Start() => GoIdle(true);

        void GoIdle(bool randomOffset)
        {
            state = State.Idle;
            timer = Random.Range(idleMin, idleMax) + (randomOffset ? Random.Range(0f, 1.5f) : 0f);
            SetY(downY);
        }

        void SetY(float y)
        {
            if (!spikes) return;
            var p = spikes.localPosition;
            p.y = y;
            spikes.localPosition = p;
        }

        void Update()
        {
            if (!GameManager.IsFighting)
            {
                if (state != State.Idle) GoIdle(true);
                return;
            }

            timer -= Time.deltaTime;
            float warnY = downY + warningRise;
            switch (state)
            {
                case State.Idle:
                    if (timer <= 0f) { state = State.Warning; timer = warningTime; }
                    break;

                case State.Warning:
                    SetY(warnY + Mathf.Sin(Time.time * 70f) * 0.03f);
                    if (timer <= 0f) { state = State.Strike; timer = strikeTime; hitThisStrike.Clear(); }
                    break;

                case State.Strike:
                    SetY(Mathf.Lerp(0f, warnY, Mathf.Clamp01(timer / strikeTime)));
                    HitPlayers();
                    if (timer <= 0f) { state = State.Hold; timer = holdTime; SetY(0f); }
                    break;

                case State.Hold:
                    if (timer <= 0f) { state = State.Retract; timer = retractTime; }
                    break;

                case State.Retract:
                    SetY(Mathf.Lerp(downY, 0f, Mathf.Clamp01(timer / retractTime)));
                    if (timer <= 0f) GoIdle(false);
                    break;
            }
        }

        void HitPlayers()
        {
            foreach (var pc in PlayerCombat.All)
            {
                if (pc.IsDead || hitThisStrike.Contains(pc)) continue;
                var local = transform.InverseTransformPoint(pc.transform.position);
                if (Mathf.Abs(local.x) > halfSize || Mathf.Abs(local.z) > halfSize) continue;
                hitThisStrike.Add(pc);
                // ReceiveHit handles dead / dodge i-frames / knocked-down; attacker null = no hit-stop on anyone else.
                pc.ReceiveHit(damage, knockback, transform.position, null, false, true);
            }
        }
    }
}
