using UnityEngine;

namespace KitFight
{
    /// <summary>Per-player input: translates the bound InputSource into motor/combat commands.</summary>
    public class PlayerInput : MonoBehaviour
    {
        public InputSource source;
        public bool controlEnabled;

        PlayerMotor motor;
        Vector2 lastMove;
        float lastMoveTime = -10f;
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

            // Remember the last direction briefly so releasing the stick a hair before pressing dodge still counts.
            if (source.Move.magnitude > 0.3f) { lastMove = source.Move; lastMoveTime = Time.time; }
            if (source.DodgeDown)
            {
                var dir = source.Move.magnitude > 0.3f ? source.Move : (Time.time - lastMoveTime < 0.2f ? lastMove : Vector2.zero);
                combat.Dodge(dir);
            }
            if (source.AttackDown) combat.Attack();
            if (source.SpecialDown) combat.SpecialAttack();
            combat.SetBlock(source.BlockHeld);
        }
    }
}
