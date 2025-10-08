using UnityEngine;
using UnityEngine.InputSystem; // <- usamos Mouse.current

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
    [Range(1f, 45f)] public float directionalMaxAngleDeg = 20f;
    [Min(0f)] public float directionalMinForward = 0.05f;

    [Header("Post Action")]
    public bool stopOnRelease = true;
    public float postActionFreezeSeconds = 0.12f;
    public float postActionSlipGlideSeconds = 0.45f;
    [Range(0f, 1f)] public float postActionSlipStartFactor = 0.6f;
    public float postActionSlipExtraDecel = 8f;

    [Header("Targets (optional, legacy)")]
    public Transform opponentGoal; // solo lo dejamos por compatibilidad (ActionShoot)

    [Header("Visual Facing (solo flip horizontal)")]
    public Transform visualRoot;
    [Range(0f, 1f)] public float horizontalFaceThreshold = 0.2f;

    // ======== NUEVO: TIRO CON PUNTERÍA ENTRE POSTES (SIN PlayerInput EVENTS) ========
    [Header("Shoot Aiming (posts)")]
    public Transform leftPost;
    public Transform rightPost;
    [Tooltip("Flecha/puntero; déjala desactivada en el prefab")]
    public Transform aimArrow;
    [Tooltip("Ciclos por segundo de la oscilación (ida y vuelta)")]
    public float aimOscillationHz = 1.2f;
    [Tooltip("Si pierdes la posesión mientras apuntas, cancelamos")]
    public bool autoCancelIfNoOwner = true;

    private bool isAiming = false;
    private float aimStartTime = 0f;
    // ===============================================================================

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

        if (aimArrow) aimArrow.gameObject.SetActive(false); // puntero oculto al inicio
    }

    void OnDestroy() => TeamRegistry.Unregister(this);

    public void SetMoveInput(Vector2 v)
    {
        if (Time.time < freezeUntil) return;
        moveInput = v;
    }

    void Update()
    {
        // ---- Flip 0°/180° según input horizontal (tu comportamiento actual) ----
        if (Mathf.Abs(moveInput.x) >= horizontalFaceThreshold && Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
        {
            var t = visualRoot ? visualRoot : transform;
            var e = t.eulerAngles;
            float z = (moveInput.x > 0f) ? 0f : 180f;
            t.rotation = Quaternion.Euler(e.x, e.y, z);
        }

        // ---- INPUT DEL MOUSE para TIRO (sin PlayerInput events) ----
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame)
                BeginAimShot();      // presionaste: empezar a apuntar
            if (mouse.rightButton.wasReleasedThisFrame)
                ConfirmAimShot();    // soltaste: disparar
        }

        // ---- Actualizar puntero si estamos apuntando ----
        if (isAiming && leftPost && rightPost && aimArrow)
        {
            float t = Mathf.PingPong((Time.time - aimStartTime) * aimOscillationHz, 1f);
            Vector3 pos = Vector3.Lerp(leftPost.position, rightPost.position, t);
            aimArrow.position = pos;

            // orientar flecha hacia la pelota (se ve más claro)
            var ball = BallController.Instance;
            if (ball)
            {
                Vector2 dir = (Vector2)pos - (Vector2)ball.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    aimArrow.rotation = Quaternion.Euler(0, 0, ang);
                }

                // cancelar si ya no soy el dueño mientras apunto
                if (autoCancelIfNoOwner && ball.Owner != this)
                    CancelAimShot();
            }
        }
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

    // ===================== PASSE / DROP (sin cambios) =====================

    public void ActionPass()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        if (stopOnRelease) Halt();

        PlayerController target = null;
        Vector2 raw = moveInput;

        if (raw.sqrMagnitude >= directionalInputThreshold * directionalInputThreshold)
        {
            Vector2 dir = Mathf.Abs(raw.x) >= Mathf.Abs(raw.y)
                ? new Vector2(Mathf.Sign(raw.x), 0f)
                : new Vector2(0f, Mathf.Sign(raw.y));

            target = TeamRegistry.GetClosestTeammateInCardinal(
                this, dir, directionalMaxAngleDeg, directionalMinForward);
        }

        if (!target) target = TeamRegistry.GetClosestTeammate(this);
        if (!target) { BallController.Instance.Pass(transform.up, passer: this); return; }

        Vector2 to = target.ballAnchor ? (Vector2)target.ballAnchor.position
                                       : (Vector2)target.transform.position;

        BallController.Instance.PassTo(to, intendedReceiver: target, passer: this);
    }

    public void ActionDrop()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;
        if (stopOnRelease) Halt();
        ball.Drop();
    }

    // ====================== TIRO ======================

    /// Legacy: tiro directo (por compatibilidad si algo lo llama)
    public void ActionShoot()
    {
        BeginAimShot();
    }

    // ---- Nuevo esquema: control interno con Mouse ----
    private void BeginAimShot()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;
        if (!leftPost || !rightPost || !aimArrow) return;

        isAiming = true;
        aimStartTime = Time.time;
        aimArrow.gameObject.SetActive(true);
    }

    private void ConfirmAimShot()
    {
        var ball = BallController.Instance;
        if (!isAiming || !ball || ball.Owner != this) { CancelAimShot(); return; }
        if (!leftPost || !rightPost) { CancelAimShot(); return; }

        float t = Mathf.PingPong((Time.time - aimStartTime) * aimOscillationHz, 1f);
        Vector2 aimPoint = Vector2.Lerp(leftPost.position, rightPost.position, t);
        Vector2 dir = (aimPoint - (Vector2)ball.transform.position).normalized;

        if (stopOnRelease) Halt();
        ball.Shoot(dir, 1f);

        CancelAimShot();
    }

    private void CancelAimShot()
    {
        isAiming = false;
        if (aimArrow) aimArrow.gameObject.SetActive(false);
    }
}
