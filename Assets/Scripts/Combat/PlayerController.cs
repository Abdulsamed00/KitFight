using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads local input and turns it into combat commands. Contains no combat rules,
/// so remote/AI characters can drive CombatController the same way.
/// </summary>
[RequireComponent(typeof(CharacterMotor), typeof(CombatController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;
    [Tooltip("Press 1-9 to swap kit (testing only).")]
    public KitDefinition[] testKits;

    public Vector2 LookInput { get; private set; }

    CharacterMotor motor;
    CombatController combat;
    InputAction move, look, light, heavy, block, dodge, ability;
    Vector3 moveDir;

    void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combat = GetComponent<CombatController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        move = new InputAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        move.AddBinding("<Gamepad>/leftStick");

        look = new InputAction("Look", InputActionType.Value);
        look.AddBinding("<Mouse>/delta");
        look.AddBinding("<Gamepad>/rightStick");

        light = new InputAction("Light", InputActionType.Button);
        light.AddBinding("<Mouse>/leftButton");
        light.AddBinding("<Gamepad>/buttonWest");

        heavy = new InputAction("Heavy", InputActionType.Button);
        heavy.AddBinding("<Keyboard>/e");
        heavy.AddBinding("<Gamepad>/buttonNorth");

        block = new InputAction("Block", InputActionType.Button);
        block.AddBinding("<Mouse>/rightButton");
        block.AddBinding("<Gamepad>/leftTrigger");

        dodge = new InputAction("Dodge", InputActionType.Button);
        dodge.AddBinding("<Keyboard>/space");
        dodge.AddBinding("<Gamepad>/buttonSouth");

        ability = new InputAction("Ability", InputActionType.Button);
        ability.AddBinding("<Keyboard>/q");
        ability.AddBinding("<Gamepad>/rightShoulder");
    }

    void OnEnable()
    {
        move.Enable(); look.Enable(); light.Enable(); heavy.Enable();
        block.Enable(); dodge.Enable(); ability.Enable();
        combat.AttackStarted += OnAttackStarted;
        LockCursor(true);
    }

    void OnDisable()
    {
        move.Disable(); look.Disable(); light.Disable(); heavy.Disable();
        block.Disable(); dodge.Disable(); ability.Disable();
        combat.AttackStarted -= OnAttackStarted;
        LockCursor(false);
    }

    void Update()
    {
        HandleCursorToggle();

        if (combat.State == CombatState.Dead)
        {
            motor.SetMove(Vector3.zero);
            LookInput = Vector2.zero;
            return;
        }

        ReadLook();
        HandleKitSwap();

        Vector2 m = move.ReadValue<Vector2>();
        Vector3 camForward = Flat(cameraTransform != null ? cameraTransform.forward : Vector3.forward);
        Vector3 camRight = Flat(cameraTransform != null ? cameraTransform.right : Vector3.right);
        moveDir = camForward * m.y + camRight * m.x;

        bool canSteer = combat.State == CombatState.Idle || combat.State == CombatState.Blocking
            || (combat.State == CombatState.Attacking && combat.Phase == AttackPhase.Recovery);
        motor.SetMove(canSteer ? moveDir : Vector3.zero);

        combat.SetBlock(block.IsPressed());
        if (light.WasPressedThisFrame()) combat.Light();
        if (heavy.WasPressedThisFrame()) combat.Heavy();
        if (ability.WasPressedThisFrame()) combat.UseAbility();
        if (dodge.WasPressedThisFrame()) combat.Dodge(moveDir);

        if (combat.State == CombatState.Blocking)
            motor.FaceTowards(camForward);
        else if (combat.State == CombatState.Idle && moveDir.sqrMagnitude > 0.01f)
            motor.FaceTowards(moveDir);
    }

    void OnAttackStarted(AttackDefinition attack)
    {
        Vector3 camForward = Flat(cameraTransform != null ? cameraTransform.forward : transform.forward);
        motor.FaceTowards(moveDir.sqrMagnitude > 0.01f ? moveDir : camForward, true);
    }

    void ReadLook()
    {
        Vector2 raw = look.ReadValue<Vector2>();
        bool gamepad = look.activeControl != null && look.activeControl.device is Gamepad;
        LookInput = gamepad ? raw * (180f * Time.deltaTime) : raw * 0.1f;
    }

    void HandleKitSwap()
    {
        if (testKits == null || Keyboard.current == null) return;
        for (int i = 0; i < testKits.Length && i < 9; i++)
        {
            var key = Keyboard.current[Key.Digit1 + i];
            if (key.wasPressedThisFrame && testKits[i] != null) combat.ApplyKit(testKits[i]);
        }
    }

    static void HandleCursorToggle()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
