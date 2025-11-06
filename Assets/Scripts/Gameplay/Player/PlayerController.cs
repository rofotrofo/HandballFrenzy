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
    [Range(1f, 60f)] public float directionalMaxAngleDeg = 20f;
    [Min(0f)] public float directionalMinForward = 0.05f;

    [Header("Post Action")]
    public bool stopOnRelease = true;
    public float postActionFreezeSeconds = 0.12f;
    public float postActionSlipGlideSeconds = 0.45f;
    [Range(0f, 1f)] public float postActionSlipStartFactor = 0.6f;
    public float postActionSlipExtraDecel = 8f;

    [Header("Shoot Aiming (posts)")]
    public Transform leftPost;
    public Transform rightPost;
    public Transform aimArrow;
    public float aimOscillationHz = 1.2f;
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

    // ===================== R O U T E R   D E   I N P U T   G L O B A L =====================

    /// <summary>
    /// Llama esto desde tu callback de movimiento del Input System.
    /// Ej: OnMove(InputAction.CallbackContext ctx) { PlayerController.SetGlobalMoveInput(ctx.ReadValue<Vector2>()); }
    /// </summary>
    public static void SetGlobalMoveInput(Vector2 v)
    {
        if (CurrentControlled != null)
            CurrentControlled.InternalSetMoveInput(v);
    }

    /// <summary>
    /// Llama desde tu callback del botón de pase (en vez de llamar a la instancia).
    /// </summary>
    public static void GlobalActionPass()
    {
        if (CurrentControlled != null)
            CurrentControlled.ActionPass();
    }

    /// <summary>
    /// Llama desde tu callback del botón de soltar.
    /// </summary>
    public static void GlobalActionDrop()
    {
        if (CurrentControlled != null)
            CurrentControlled.ActionDrop();
    }

    /// <summary>
    /// Llama desde tu callback del botón de tiro.
    /// </summary>
    public static void GlobalActionShoot()
    {
        if (CurrentControlled != null)
            CurrentControlled.ActionShoot();
    }

    // =======================================================================================

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

    // Input de movimiento (WASD) si te siguen llamando por instancia (back-compat).
    // Lo dejamos, pero la recomendación es usar SetGlobalMoveInput.
    public void SetMoveInput(Vector2 v)
    {
        if (this != CurrentControlled) return;
        InternalSetMoveInput(v);
    }

    private void InternalSetMoveInput(Vector2 v)
    {
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
        // 1) Auto-switch de control al receptor de tu equipo al cambiar posesión real
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

        // 3) Disparo humano con mouse SOLO si este es el jugador controlado
        if (this == CurrentControlled)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) BeginAimShot();
                if (mouse.rightButton.wasReleasedThisFrame) ConfirmAimShot();
            }
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

    // ===================== PASS / DROP (HUMANO) =====================

    public void ActionPass()
    {
        if (this != CurrentControlled) return;

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

        // Si no encontramos receptor, pase genérico hacia adelante
        if (!target)
        {
            ball.Pass(transform.up, passer: this);
            return;
        }

        // Cambiar control inmediatamente al receptor previsto (equipo humano)
        ForceSwitchControlTo(target);

        Vector2 to = target.ballAnchor ? (Vector2)target.ballAnchor.position
                                       : (Vector2)target.transform.position;

        ball.PassTo(to, intendedReceiver: target, passer: this);
    }

    public void ActionDrop()
    {
        if (this != CurrentControlled) return;

        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;
        if (stopOnRelease) Halt();
        ball.Drop();
    }

    // ====================== SHOOT HUMANO ======================

    public void ActionShoot()
    {
        if (this != CurrentControlled) return;
        BeginAimShot();
    }

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
        if (!isAiming || this != CurrentControlled) { CancelAimShot(); return; }

        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) { CancelAimShot(); return; }
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

    // ====================== MÉTODOS PARA IA ======================

    public void AIPass(Vector2 preferredDir, float coneAngleDeg = 25f, float minForward = 0.0f)
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        if (stopOnRelease) Halt();

        PlayerController target = null;

        if (preferredDir.sqrMagnitude > 0.0001f)
        {
            target = TeamRegistry.GetClosestTeammateInCardinal(
                this, preferredDir.normalized, coneAngleDeg, minForward);
        }

        if (!target)
            target = TeamRegistry.GetClosestTeammate(this);

        if (!target)
        {
            Vector2 dir = (preferredDir.sqrMagnitude > 0.0001f) ? preferredDir.normalized : transform.right;
            ball.Pass(dir, passer: this);
            return;
        }

        // Pre-switch si el receptor es del equipo humano
        ForceSwitchControlIfHumanTeam(target);

        Vector2 to = target.ballAnchor ? (Vector2)target.ballAnchor.position
                                       : (Vector2)target.transform.position;

        ball.PassTo(to, intendedReceiver: target, passer: this);
    }

    public void AIShootAtGoal(float spread = 0.12f, float power = 1f)
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;
        if (!leftPost || !rightPost)
        {
            AIPass(transform.right, 20f, 0f);
            return;
        }

        float t = Mathf.Clamp01(0.5f + Random.Range(-spread, spread));
        Vector2 aimPoint = Vector2.Lerp(leftPost.position, rightPost.position, t);
        Vector2 dir = (aimPoint - (Vector2)ball.transform.position).normalized;

        if (stopOnRelease) Halt();
        ball.Shoot(dir, power);
    }

    // ====================== HELPERS DE CONTROL ======================

    private static void ForceSwitchControlTo(PlayerController p)
    {
        if (p == null) return;
        if (_humanTeamSet && !p.teamId.Equals(_humanTeamId)) return;
        CurrentControlled = p;
    }

    private static void ForceSwitchControlIfHumanTeam(PlayerController p)
    {
        if (p == null) return;
        if (_humanTeamSet && p.teamId.Equals(_humanTeamId))
            CurrentControlled = p;
    }
}
