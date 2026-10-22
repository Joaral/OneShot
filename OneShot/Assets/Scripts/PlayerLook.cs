using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mirada en primera persona.
///  - Yaw (horizontal): rota el cuerpo del Player.
///  - Pitch (vertical): rota solo el pivote de la cámara, con límites.
/// El delta del ratón NO se multiplica por deltaTime: ya es un desplazamiento, no una velocidad.
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerLook : MonoBehaviour
{
    [SerializeField] Transform cameraPivot;
    [Tooltip("Grados por píxel de movimiento del ratón.")]
    [SerializeField] float sensitivity = 0.1f;
    [SerializeField] float maxPitch = 89f;

    PlayerInputReader input;
    float pitch;

    void Awake()
    {
        input = GetComponent<PlayerInputReader>();
    }

    void OnEnable()
    {
        SetCursorLocked(true);
    }

    void Update()
    {
        // Escape libera el cursor, clic lo vuelve a bloquear (cómodo en el editor).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);
        if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = input.Look * sensitivity;

        transform.Rotate(0f, delta.x, 0f, Space.Self);

        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
