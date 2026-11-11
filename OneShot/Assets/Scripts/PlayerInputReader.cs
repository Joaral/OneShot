using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    InputAction move;
    InputAction look;
    InputAction jump;
    InputAction fire;

    
    public Vector2 Move => move.ReadValue<Vector2>();
    public Vector2 Look => look.ReadValue<Vector2>();
    public bool JumpHeld => jump.IsPressed();
    public bool FireHeld => fire.IsPressed();

    
    public bool JumpPressed => jump.WasPressedThisFrame();
    public bool JumpReleased => jump.WasReleasedThisFrame();
    public bool FirePressed => fire.WasPressedThisFrame();

    void Awake()
    {
        move = new InputAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        move.AddBinding("<Gamepad>/leftStick");

        look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");

        jump = new InputAction("Jump", InputActionType.Button);
        jump.AddBinding("<Keyboard>/space");
        jump.AddBinding("<Gamepad>/buttonSouth");

        fire = new InputAction("Fire", InputActionType.Button);
        fire.AddBinding("<Mouse>/leftButton");
        fire.AddBinding("<Gamepad>/rightTrigger");
    }

    void OnEnable()
    {
        move.Enable();
        look.Enable();
        jump.Enable();
        fire.Enable();
    }

    void OnDisable()
    {
        move.Disable();
        look.Disable();
        jump.Disable();
        fire.Disable();
    }

    void OnDestroy()
    {
        move.Dispose();
        look.Dispose();
        jump.Dispose();
        fire.Dispose();
    }
}
