using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
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
    [Tooltip("Apertura del cono direccional para encontrar compañero (grados).")]
    [Range(1f, 60f)] public float directionalMaxAngleDeg = 20f;
    [Tooltip("Componente mínima hacia adelante (0-1) para considerar a un compañero en el cono.")]
    [Min(0f)] public float directionalMinForward = 0.05f;

    [Header("Post Action")]
    public bool stopOnRelease = true;
    public float postActionFreezeSeconds = 0.12f;
    public float postActionSlipGlideSeconds = 0.45f;
    [Range(0f, 1f)] public float postActionSlipStartFactor = 0.6f;
    public float postActionSlipExtraDecel = 8f;

    // ---- TIRO CON PUNTERÍA ENTRE POSTES ----
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

    [Header("Selection Indicator")]
    public GameObject hasBall;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float freezeUntil = -1f;
    private bool glideActive = false;
    private float glideUntil = -1f;

    // Jugador actualmente controlado (para indicador visual)
    public static PlayerController CurrentControlled { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        TeamRegistry.Register(this);

        if (aimArrow) aimArrow.gameObject.SetActive(false);
        if (CurrentControlled == null) CurrentControlled = this;
    }

    void OnDestroy() => TeamRegistry.Unregister(this);

    // Input de movimiento (WASD)
    public void SetMoveInput(Vector2 v)
    {
        CurrentControlled = this;           // marcar como controlado aunque esté congelado
        if (Time.time < freezeUntil) return;
        moveInput = v;
    }

    void Update()
    {
        // Flip lateral por escala (evita rotar sprite)
        if (Mathf.Abs(moveInput.x) > 0.1f && Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
        {
            var scale = transform.localScale;
            float targetX = Mathf.Sign(moveInput.x) * Mathf.Abs(scale.x);
            if (!Mathf.Approximately(scale.x, targetX))
            {
                scale.x = targetX;
                transform.localScale = scale;
            }
        }

        // Disparo: click derecho para empezar/soltar
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame) BeginAimShot();
            if (mouse.rightButton.wasReleasedThisFrame) ConfirmAimShot();
        }

        // Actualizar puntero de tiro
        var ball = BallController.Instance;
        if (isAiming && leftPost && rightPost && aimArrow)
        {
            float t = Mathf.PingPong((Time.time - aimStartTime) * aimOscillationHz, 1f);
            Vector3 pos = Vector3.Lerp(leftPost.position, rightPost.position, t);
            if ((aimArrow.position - pos).sqrMagnitude > 0.000001f)
                aimArrow.position = pos;

            if (ball)
            {
                Vector2 dir = (Vector2)pos - (Vector2)ball.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    aimArrow.rotation = Quaternion.Euler(0f, 0f, ang);
                }

                if (autoCancelIfNoOwner && ball.Owner != this)
                    CancelAimShot();
            }
        }

        // Indicador: jugador actualmente controlado
        if (hasBall)
        {
            bool isSelected = (CurrentControlled == this);
            if (hasBall.activeSelf != isSelected)
                hasBall.SetActive(isSelected);
        }
    }

    void FixedUpdate()
    {
        if (MatchTimer.CountdownActive)
        {
            rb.linearVelocity = Vector2.zero; // o tu forma de detener
            return;
        }

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
            else
            {
                glideActive = false;
                rb.linearVelocity = Vector2.zero;
            }
        }
        else
        {
            glideActive = false;
            rb.linearVelocity = Vector2.zero;
        }

        moveInput = Vector2.zero;
    }

    // ===================== PASS / DROP =====================

    public void ActionPass()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        if (stopOnRelease) Halt();

        PlayerController target = null;
        bool hasDirectionalInput = moveInput.sqrMagnitude > 0.0001f;

        if (hasDirectionalInput)
        {
            Vector2 dir = moveInput.normalized;
            target = TeamRegistry.GetClosestTeammateInCardinal(
                this, dir, directionalMaxAngleDeg, directionalMinForward);
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

    public void ActionDrop()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;
        if (stopOnRelease) Halt();
        ball.Drop();
    }

    // ====================== SHOOT ======================

    public void ActionShoot() => BeginAimShot();

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
        if (aimArrow && aimArrow.gameObject.activeSelf) aimArrow.gameObject.SetActive(false);
    }
}
