using UnityEngine;

namespace KitFight
{
    /// <summary>Per-player input: translates the bound InputSource into motor/combat commands.</summary>
    public class PlayerInput : MonoBehaviour
    {
        public InputSource source;
        public bool controlEnabled;

        PlayerMotor motor;
        PlayerCombat combat;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
        }

        void Update()
        {
            if (source == null || !controlEnabled || combat.IsDead)
            {
                motor.SetMove(Vector2.zero, PlayerMotor.Gait.Run);
                combat.SetBlock(false);
                return;
            }

            source.Poll();

            var gait = PlayerMotor.Gait.Run;
            if (source.Sprint) gait = PlayerMotor.Gait.Sprint;
            else if (source.kind == InputSource.Kind.Gamepad && source.Move.magnitude < 0.55f) gait = PlayerMotor.Gait.Walk;
            motor.SetMove(source.Move, gait);

            if (source.AttackDown) combat.Attack();
            combat.SetBlock(source.BlockHeld);
        }
    }
}
