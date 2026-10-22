using UnityEngine;

/// <summary>
/// Controlador cinemático en primera persona: sin Rigidbody, sin fuerzas.
/// Todo el movimiento sale de velocidades propias + consultas espaciales (casts).
///
/// REQUISITOS DE ESCENA:
///  - El pivote del objeto Player está en los PIES.
///  - CapsuleCollider con center = (0, height/2, 0). El motor lee radio y altura de ahí.
///  - El Player debe estar en su propia Layer (ej. "Player") y esa layer
///    DEBE estar desmarcada en collisionMask, o los casts chocarán contra ti mismo.
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Colisiones")]
    [Tooltip("Layers contra las que se colisiona. Quita la layer del propio Player.")]
    [SerializeField] LayerMask collisionMask = ~0;
    [Tooltip("Margen de seguridad para no quedar pegado/incrustado en superficies.")]
    [SerializeField] float skin = 0.015f;
    [Tooltip("Iteraciones del collide-and-slide (esquinas, rincones).")]
    [SerializeField] int maxBounces = 4;

    [Header("Movimiento")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float groundAcceleration = 60f;
    [SerializeField] float airAcceleration = 20f;

    [Header("Suelo")]
    [SerializeField] float slopeLimit = 45f;
    [Tooltip("Distancia máxima a la que se 'pega' al suelo (rampas descendentes).")]
    [SerializeField] float groundCheckDistance = 0.1f;

    [Header("Salto (se define por altura y tiempo, no por 'fuerza')")]
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float timeToApex = 0.4f;
    [Tooltip("Gravedad extra al caer. >1 hace el salto menos flotante.")]
    [SerializeField] float fallGravityMultiplier = 1.6f;
    [Tooltip("Al soltar el botón en la subida, la velocidad vertical se multiplica por esto.")]
    [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.5f;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.12f;
    [SerializeField] float terminalVelocity = 40f;

    // --- Estado público de solo lectura (útil para animaciones, sonidos, UI...) ---
    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

    // --- Estado interno ---
    PlayerInputReader input;
    float radius;
    float height;

    Vector3 horizontalVelocity;
    float verticalVelocity;

    float gravity;
    float jumpSpeed;

    float coyoteTimer;
    float bufferTimer;
    bool isJumping;

    void Awake()
    {
        input = GetComponent<PlayerInputReader>();

        var capsule = GetComponent<CapsuleCollider>();
        radius = capsule.radius;
        height = capsule.height;

        RecalculateJumpPhysics();
    }

    void OnValidate()
    {
        RecalculateJumpPhysics();
    }

    /// <summary>
    /// De "quiero saltar h metros y tardar t segundos en llegar arriba":
    ///   gravedad = 2h / t²      velocidad inicial = 2h / t
    /// Así ajustas el salto con valores que entiendes, no con números mágicos.
    /// </summary>
    void RecalculateJumpPhysics()
    {
        timeToApex = Mathf.Max(0.01f, timeToApex);
        gravity = 2f * jumpHeight / (timeToApex * timeToApex);
        jumpSpeed = 2f * jumpHeight / timeToApex;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        CheckGround();
        HandleJump(dt);
        HandleHorizontalVelocity(dt);

        Vector3 delta = ComputeHorizontalDelta() + ComputeVerticalDelta(dt);

        transform.position = MoveAndSlide(transform.position, delta, out bool hitCeiling);

        // Si chocamos con un techo mientras subíamos, cortamos la subida.
        if (hitCeiling && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    // ------------------------------------------------------------------
    //  SUELO
    // ------------------------------------------------------------------

    void CheckGround()
    {
        IsGrounded = false;
        GroundNormal = Vector3.up;

        // Si estamos subiendo (acabamos de saltar) no hay nada que comprobar.
        if (verticalVelocity > 0f) return;

        // Lanzamos la esfera desde ligeramente por encima de los pies para no
        // empezar solapados con el suelo (un cast que empieza dentro no detecta ese collider).
        Vector3 origin = transform.position + Vector3.up * (radius + skin);
        float castDistance = groundCheckDistance + 2f * skin;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit,
                               castDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle <= slopeLimit)
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                isJumping = false;

                // Ground snapping: pegamos los pies al suelo dejando el margen 'skin'.
                // Evita que el jugador "vuele a saltitos" al bajar rampas.
                float snap = hit.distance - 2f * skin;
                transform.position += Vector3.down * snap;
            }
        }
    }

    // ------------------------------------------------------------------
    //  SALTO
    // ------------------------------------------------------------------

    void HandleJump(float dt)
    {
        // Coyote time: seguimos "contando como suelo" unos ms tras salir de un borde.
        if (IsGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= dt;

        // Jump buffer: recordamos la pulsación unos ms por si aún no hemos aterrizado.
        if (input.JumpPressed) bufferTimer = jumpBufferTime;
        else bufferTimer -= dt;

        if (bufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = jumpSpeed;
            IsGrounded = false;
            isJumping = true;
            bufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // Altura variable: soltar el botón pronto acorta el salto.
        if (input.JumpReleased && isJumping && verticalVelocity > 0f)
            verticalVelocity *= jumpCutMultiplier;
    }

    // ------------------------------------------------------------------
    //  VELOCIDADES
    // ------------------------------------------------------------------

    void HandleHorizontalVelocity(float dt)
    {
        Vector2 move = input.Move;

        // transform.forward/right son horizontales porque el Player solo rota en yaw.
        Vector3 wish = transform.right * move.x + transform.forward * move.y;
        wish = Vector3.ClampMagnitude(wish, 1f) * moveSpeed;

        float accel = IsGrounded ? groundAcceleration : airAcceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish, accel * dt);
    }

    Vector3 ComputeHorizontalDelta()
    {
        // Parte horizontal; velocidad (m/s) -> desplazamiento (m)
        Vector3 delta = horizontalVelocity * Time.deltaTime;

        // En suelo, proyectamos sobre la pendiente para subir/bajar rampas
        // manteniendo la misma velocidad sobre la superficie.
        if (IsGrounded)
        {
            float mag = delta.magnitude;
            delta = Vector3.ProjectOnPlane(delta, GroundNormal).normalized * mag;
        }
        return delta;
    }

    Vector3 ComputeVerticalDelta(float dt)
    {
        if (IsGrounded)
        {
            verticalVelocity = 0f;
            return Vector3.zero;
        }

        // Gravedad más fuerte al caer que al subir.
        float g = verticalVelocity < 0f ? gravity * fallGravityMultiplier : gravity;

        float newVelocity = Mathf.Max(verticalVelocity - g * dt, -terminalVelocity);

        // Integración con la velocidad media del frame: da el mismo salto
        // a 30, 60 o 144 FPS (con Euler simple la altura variaría con el framerate).
        float displacement = (verticalVelocity + newVelocity) * 0.5f * dt;

        verticalVelocity = newVelocity;
        return Vector3.up * displacement;
    }

    // ------------------------------------------------------------------
    //  COLLIDE AND SLIDE
    // ------------------------------------------------------------------

    Vector3 MoveAndSlide(Vector3 position, Vector3 delta, out bool hitCeiling)
    {
        hitCeiling = false;

        for (int i = 0; i < maxBounces && delta.sqrMagnitude > 1e-8f; i++)
        {
            Vector3 dir = delta.normalized;
            float dist = delta.magnitude;

            GetCapsulePoints(position, out Vector3 p1, out Vector3 p2);

            if (Physics.CapsuleCast(p1, p2, radius, dir, out RaycastHit hit,
                                    dist + skin, collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Avanzamos hasta el impacto, dejando el margen skin.
                float travel = Mathf.Max(0f, hit.distance - skin);
                position += dir * travel;

                if (hit.normal.y < -0.5f) hitCeiling = true;

                // Lo que queda por recorrer se proyecta sobre la superficie: resbalamos.
                Vector3 remaining = dir * (dist - travel);
                delta = Vector3.ProjectOnPlane(remaining, hit.normal);
            }
            else
            {
                position += delta;
                break;
            }
        }
        return position;
    }

    /// <summary>Centros de las dos semiesferas de la cápsula (pivote en los pies).</summary>
    void GetCapsulePoints(Vector3 position, out Vector3 bottom, out Vector3 top)
    {
        bottom = position + Vector3.up * radius;
        top = position + Vector3.up * (height - radius);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) return;

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + Vector3.up * (capsule.radius + skin);
        Gizmos.DrawWireSphere(origin + Vector3.down * (groundCheckDistance + 2f * skin), capsule.radius);
    }
#endif
}
