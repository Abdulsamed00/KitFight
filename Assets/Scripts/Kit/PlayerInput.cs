using UnityEngine;
using UnityEngine.InputSystem;

namespace KitFight
{
    /// <summary>Local input only: translates keys into motor/combat/kit commands.</summary>
    public class PlayerInput : MonoBehaviour
    {
        PlayerMotor motor;
        PlayerCombat combat;
        KitSwitcher switcher;

        static readonly Key[] KitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
            switcher = GetComponent<KitSwitcher>();
        }

        void Start() => SetCursor(true);

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) SetCursor(false);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursor(true);
                return; // this click only recaptures the cursor
            }

            Vector2 move = Vector2.zero;
            if (kb.wKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed) move.x -= 1f;

            var gait = PlayerMotor.Gait.Run;
            if (kb.leftShiftKey.isPressed) gait = PlayerMotor.Gait.Sprint;
            else if (kb.leftCtrlKey.isPressed) gait = PlayerMotor.Gait.Walk;
            motor.SetMove(move, gait);

            if (mouse != null && mouse.leftButton.wasPressedThisFrame) combat.Attack();

            for (int i = 0; i < KitKeys.Length; i++)
                if (kb[KitKeys[i]].wasPressedThisFrame) switcher.Select(i);
        }
    }
}
