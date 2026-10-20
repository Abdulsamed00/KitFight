using UnityEngine;
using UnityEngine.InputSystem;

namespace KitFight
{
    /// <summary>
    /// One physical control scheme (keyboard half or a gamepad), polled once per frame.
    /// Players are bound to a source in the lobby, so any mix of devices works.
    /// </summary>
    public class InputSource
    {
        public enum Kind { Keyboard1, Keyboard2, Gamepad }

        public readonly Kind kind;
        public readonly Gamepad pad;

        public Vector2 Move;
        public bool Sprint;
        public bool AttackDown;   // also "confirm" in menus
        public bool BlockHeld;
        public bool BlockDown;    // also "back" in menus
        public bool NavLeft, NavRight;

        int lastFrame = -1;
        float prevX;

        public InputSource(Kind kind, Gamepad pad = null)
        {
            this.kind = kind;
            this.pad = pad;
        }

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case Kind.Keyboard1: return "Klavye 1";
                    case Kind.Keyboard2: return "Klavye 2";
                    default: return "Gamepad";
                }
            }
        }

        public string AttackHint
        {
            get
            {
                switch (kind)
                {
                    case Kind.Keyboard1: return "J / Space";
                    case Kind.Keyboard2: return ", / Numpad1";
                    default: return "A / X / RT";
                }
            }
        }

        public string ControlsText
        {
            get
            {
                switch (kind)
                {
                    case Kind.Keyboard1: return "WASD hareket | Shift sprint | J/Space saldiri | K blok";
                    case Kind.Keyboard2: return "Oklar hareket | R-Shift sprint | , veya Numpad1 saldiri | . veya Numpad2 blok";
                    default: return "Sol analog hareket | RB/L3 sprint | A,X,RT saldiri | LT,LB,B blok";
                }
            }
        }

        public void Poll()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            Move = Vector2.zero;
            Sprint = AttackDown = BlockHeld = BlockDown = false;

            switch (kind)
            {
                case Kind.Keyboard1: PollKeyboard1(); break;
                case Kind.Keyboard2: PollKeyboard2(); break;
                default: PollPad(); break;
            }

            NavRight = Move.x > 0.6f && prevX <= 0.6f;
            NavLeft = Move.x < -0.6f && prevX >= -0.6f;
            prevX = Move.x;
        }

        void PollKeyboard1()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            Move = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                               (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            Sprint = kb.leftShiftKey.isPressed;
            AttackDown = kb.jKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            BlockHeld = kb.kKey.isPressed;
            BlockDown = kb.kKey.wasPressedThisFrame;
        }

        void PollKeyboard2()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            Move = new Vector2((kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0),
                               (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0));
            Sprint = kb.rightShiftKey.isPressed || kb.numpad0Key.isPressed;
            AttackDown = kb.commaKey.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            BlockHeld = kb.periodKey.isPressed || kb.numpad2Key.isPressed;
            BlockDown = kb.periodKey.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
        }

        void PollPad()
        {
            if (pad == null) return;
            Move = pad.leftStick.ReadValue();
            if (Move.sqrMagnitude < 0.01f) Move = pad.dpad.ReadValue();
            Sprint = pad.leftStickButton.isPressed || pad.rightShoulder.isPressed;
            AttackDown = pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;
            BlockHeld = pad.leftTrigger.isPressed || pad.leftShoulder.isPressed || pad.buttonEast.isPressed;
            BlockDown = pad.leftTrigger.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame;
        }
    }
}
