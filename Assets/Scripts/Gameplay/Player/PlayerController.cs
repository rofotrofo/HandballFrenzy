using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float speed = 5f;

    [Header("Team")]
    public TeamId teamId = TeamId.Blue;

    [Header("Ball Anchor")]
    public Transform ballAnchor;

    [Header("Surface Modifiers (runtime)")]
    [Range(0.1f, 2f)] public float surfaceMultiplier = 1f;
    public bool slipEnabled = false;
    public float slipAccel = 18f;
    public float slipDecel = 3f;

    [Header("Directional Pass")]
    [Range(0f, 1f)] public float directionalInputThreshold = 0.2f;
    [Tooltip("Tolerancia angular del sector cardinal (ej. 20°)")]
    [Range(1f, 45f)] public float directionalMaxAngleDeg = 20f;
    [Tooltip("Avance mínimo en el eje elegido para contar como hacia 'adelante' (metros)")]
    [Min(0f)] public float directionalMinForward = 0.05f;

    [Header("Post Action")]
    public bool stopOnRelease = true;
    public float postActionFreezeSeconds = 0.12f;
    public float postActionSlipGlideSeconds = 0.45f;
    [Range(0f, 1f)] public float postActionSlipStartFactor = 0.6f;
    public float postActionSlipExtraDecel = 8f;

    [Header("Targets (optional)")]
    public Transform opponentGoal;

    [Header("Visual Facing (solo flip horizontal)")]
    [Tooltip("Si lo asignas, solo este hijo gira 0/180°. Si está vacío, rota el propio Player.")]
    public Transform visualRoot;
    [Tooltip("Umbral de input horizontal para decidir giro (0.15–0.25 recomendado)")]
    [Range(0f, 1f)] public float horizontalFaceThreshold = 0.2f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float freezeUntil = -1f;
    private bool glideActive = false;
    private float glideUntil = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        TeamRegistry.Register(this);
    }

    void OnDestroy() => TeamRegistry.Unregister(this);

    public void SetMoveInput(Vector2 v)
    {
        if (Time.time < freezeUntil) return;
        moveInput = v;
    }

    // --- Flip 0°/180° según input horizontal (no rota al iniciar ni con input vertical) ---
    void Update()
    {
        // Horizontal dominante y por encima del umbral -> girar
        if (Mathf.Abs(moveInput.x) >= horizontalFaceThreshold && Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
        {
            var t = visualRoot ? visualRoot : transform;
            var e = t.eulerAngles;
            float z = (moveInput.x > 0f) ? 0f : 180f; // derecha = 0°, izquierda = 180°
            t.rotation = Quaternion.Euler(e.x, e.y, z);
        }
        // Si el input es vertical o menor al umbral, NO cambiamos la rotación
    }

    void FixedUpdate()
    {
        if (!rb || !rb.simulated) return;

        bool inFreeze = Time.time < freezeUntil;
        if (inFreeze)
        {
            if (slipEnabled && glideActive)
            {
                float extra = (slipDecel + postActionSlipExtraDecel) * Time.fixedDeltaTime;
                rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, extra);
                if (Time.time >= glideUntil) { glideActive = false; rb.linearVelocity = Vector2.zero; }
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        Vector2 target = moveInput * (speed * surfaceMultiplier);
        if (!slipEnabled)
        {
            rb.linearVelocity = target;
        }
        else
        {
            bool hasInput = moveInput.sqrMagnitude > 0.0001f;
            float rate = (hasInput ? slipAccel : slipDecel) * Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate);
        }
    }

    public void SetSlip(bool enabled, float accel = 18f, float decel = 3f)
    {
        slipEnabled = enabled;
        slipAccel = accel;
        slipDecel = decel;
    }

    private void Halt()
    {
        if (postActionFreezeSeconds > 0f)
            freezeUntil = Time.time + postActionFreezeSeconds;

        if (slipEnabled)
        {
            rb.linearVelocity *= postActionSlipStartFactor;
            if (postActionSlipGlideSeconds > 0f && rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                glideActive = true;
                glideUntil = Time.time + postActionSlipGlideSeconds;
            }
            else { glideActive = false; rb.linearVelocity = Vector2.zero; }
        }
        else
        {
            glideActive = false;
            rb.linearVelocity = Vector2.zero;
        }
        moveInput = Vector2.zero;
    }

    // ===== ACCIONES =====

    public void ActionPass()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        if (stopOnRelease) Halt();

        PlayerController target = null;
        Vector2 raw = moveInput;

        if (raw.sqrMagnitude >= directionalInputThreshold * directionalInputThreshold)
        {
            // Cardinal estricta por componente dominante
            Vector2 dir = Mathf.Abs(raw.x) >= Mathf.Abs(raw.y)
                ? new Vector2(Mathf.Sign(raw.x), 0f)   // izquierda/derecha
                : new Vector2(0f, Mathf.Sign(raw.y));  // abajo/arriba

            target = TeamRegistry.GetClosestTeammateInCardinal(
                this,
                dir,
                directionalMaxAngleDeg,   // tolerancia angular pequeña (p.ej. 20°)
                directionalMinForward     // avance mínimo en ese eje
            );
        }

        if (!target)
            target = TeamRegistry.GetClosestTeammate(this);

        if (!target)
        {
            ball.Pass(transform.up, passer: this);
            return;
        }

        Vector2 to = target.ballAnchor ? (Vector2)target.ballAnchor.position
                                       : (Vector2)target.transform.position;

        ball.PassTo(to, intendedReceiver: target, passer: this);
    }

    public void ActionShoot()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        if (stopOnRelease) Halt();

        Vector2 dir = opponentGoal
            ? ((Vector2)opponentGoal.position - (Vector2)ball.transform.position).normalized
            : (Vector2)transform.up;

        ball.Shoot(dir, 1f);
    }

    public void ActionDrop()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        if (stopOnRelease) Halt();
        ball.Drop();
    }
}
