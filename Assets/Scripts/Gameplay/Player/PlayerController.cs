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
    public GameObject hasBall; // indicador de jugador seleccionado

    private Rigidbody2D rb;

    // Input del jugador controlado
    private Vector2 moveInput;

    // Input de IA para compañeros
    private Vector2 aiMoveInput;

    // Usa el input correcto según si es el jugador activo o un bot
    private Vector2 EffectiveInput => (this == CurrentControlled) ? moveInput : aiMoveInput;

    private float freezeUntil = -1f;
    private bool glideActive = false;
    private float glideUntil = -1f;

    // === Control humano e identificación de equipo humano ===
    public static PlayerController CurrentControlled { get; private set; }
    private static PlayerController _lastBallOwner;
    private static TeamId _humanTeamId;
    private static bool _humanTeamSet = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        TeamRegistry.Register(this);

        if (aimArrow) aimArrow.gameObject.SetActive(false);

        // Primer jugador que se registre como controlado fija el equipo humano
        if (CurrentControlled == null)
        {
            CurrentControlled = this;
            if (!_humanTeamSet)
            {
                _humanTeamId = teamId;
                _humanTeamSet = true;
            }
        }
    }

    void OnDestroy() => TeamRegistry.Unregister(this);

    // Input de movimiento (WASD) del jugador humano
    public void SetMoveInput(Vector2 v)
    {
        CurrentControlled = this; // este pasa a ser el controlado
        if (!_humanTeamSet)
        {
            _humanTeamId = teamId;
            _humanTeamSet = true;
        }

        if (Time.time < freezeUntil) return;
        moveInput = v;
    }

    // Input de IA (no cambia CurrentControlled)
    public void SetAIMoveInput(Vector2 v)
    {
        aiMoveInput = v;
    }

    void Update()
    {
        // 1) Auto-switch de control al receptor de tu equipo al cambiar posesión
        var ball = BallController.Instance;
        var owner = (ball != null) ? ball.Owner : null;

        if (owner != _lastBallOwner)
        {
            _lastBallOwner = owner;

            // Si la posesión la toma alguien de TU equipo, cambia el control
            if (owner != null && _humanTeamSet && owner.teamId.Equals(_humanTeamId))
            {
                CurrentControlled = owner;
            }

            // Si se pierde la posesión o la toma el rival, no cambiamos el control
            // (Puedes agregar lógica para auto-seleccionar al más cercano en defensa si lo deseas)
        }

        // 2) Flip lateral por escala usando EffectiveInput
        var eff = EffectiveInput;
        if (Mathf.Abs(eff.x) > 0.1f && Mathf.Abs(eff.x) >= Mathf.Abs(eff.y))
        {
            var scale = transform.localScale;
            float targetX = Mathf.Sign(eff.x) * Mathf.Abs(scale.x);
            if (!Mathf.Approximately(scale.x, targetX))
            {
                scale.x = targetX;
                transform.localScale = scale;
            }
        }

        // 3) Disparo: click derecho para empezar/soltar
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame) BeginAimShot();
            if (mouse.rightButton.wasReleasedThisFrame) ConfirmAimShot();
        }

        // 4) Actualizar puntero de tiro
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

        // 5) Indicador: jugador actualmente controlado
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
            rb.linearVelocity = Vector2.zero;
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

        Vector2 eff = EffectiveInput;
        Vector2 target = eff * (speed * surfaceMultiplier);

        if (!slipEnabled)
        {
            rb.linearVelocity = target;
        }
        else
        {
            bool hasInput = eff.sqrMagnitude > 0.0001f;
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

        // Resetear input al terminar acción
        moveInput = Vector2.zero;
        aiMoveInput = Vector2.zero;
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
        // El switch de control ocurrirá cuando el BallController fije Owner en el receptor
        // (lo detectamos en Update por cambio de _lastBallOwner).
    }

    public void ActionDrop()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;
        if (stopOnRelease) Halt();
        ball.Drop();
        // No cambiamos control aquí; puedes añadir lógica si lo prefieres.
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
