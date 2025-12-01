using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    public static BallController Instance { get; private set; }

    [Header("Speeds")]
    [Min(0f)] public float passSpeed = 9f;
    [Min(0f)] public float shotSpeed = 13f;

    [Header("Stick-to-owner")]
    [Min(0f)] public float stickSmoothing = 25f;

    // ---------- SMART PICKUP V3 ----------
    [Header("Pickup PRO (Smart V3)")]
    [SerializeField, Min(0f)] private float pickupBlockSeconds = 0.2f;
    [SerializeField, Min(0f)] private float pickupRadius = 0.55f;
    [SerializeField, Min(0f)] private float pickupWallBuffer = 0.15f;
    [SerializeField, Min(0f)] private float pickupCooldown = 0.05f;

    [Tooltip("Permite recibir pase desde atrás sin girar al jugador")]
    [SerializeField] private bool allowBackPickup = true;

    private float lastPickupTime = -1f;

    // ----------------------------------------------------------

    [Header("Colliders")]
    public Collider2D solidCollider;
    public Collider2D triggerCollider;

    [Header("Pass Ghost (per receptor)")]
    public float passGhostExtraBuffer = 0.15f;
    public float passGhostMax = 1.0f;
    public float passGhostMin = 0.15f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color stoneColor = new Color(0.6f, 0.6f, 0.6f);

    // -------- Ghost Ball ----------
    [Header("Ghost Ball")]
    public string ballLayerName = "Ball";
    public string defaultEnemyLayerName = "Enemy";
    public string enemyTag = "Enemy";
    public bool ghostIgnorePerCollider = true;
    public bool disableSolidColliderDuringGhost = false;
    [Range(0f, 1f)] public float ghostAlpha = 0.45f;

    private float _ghostActiveUntil = -1f;
    private int _ghostBallLayer = -1;
    private int _ghostEnemyLayer = -1;
    private bool _ghostApplied = false;

    private Color _originalColor;
    private bool _ghostSolidDisabled = false;
    private bool _solidPrevEnabled = true;
    private readonly List<Collider2D> _ghostIgnoredEnemyColliders = new();

    // -------- Stone Ball ----------
    public enum BallMode { Normal, Stone }

    [Header("Stone Ball (runtime)")]
    [SerializeField] private BallMode _mode = BallMode.Normal;
    [SerializeField] private float _stoneShotMul = 1.6f;
    [SerializeField] private float _stonePassMul = 1.3f;
    [SerializeField] private float _stoneReceiverKnockbackImpulse = 8f;
    [SerializeField] private float _stoneUntil = -1f;

    private enum LastLaunch { None, Pass, Shoot }
    private LastLaunch _lastLaunch = LastLaunch.None;
    private Vector2 _lastLinearVelocityBeforeCatch = Vector2.zero;

    // -------- Shockwave Power-Up ----------
    [Header("Shockwave Config")]
    public bool shockwaveArmed = false;
    public float innerRadius = 0.8f;
    public float outerRadius = 3.0f;
    public float shockwaveDuration = 0.4f;
    public float impulseMax = 12f;
    public float impulseMin = 3f;
    public LayerMask affectMask;
    public bool ignoreOwner = true;

    private bool shockwaveRunning = false;
    private HashSet<int> _pushedIds = new();

    private Rigidbody2D rb;
    public PlayerController Owner { get; private set; }
    public event Action<PlayerController> OnOwnerChanged;
    private float pickupBlockUntil;
    private bool IsPossessed => Owner != null;

    private readonly List<Collider2D> _temporarilyIgnored = new();
    private float _passGhostEndsAt = -1f;
    private PlayerController _intendedReceiver = null;

    // PAUSE
    private bool _isPaused = false;
    private Coroutine _currentShockwaveCoroutine;

    // -------------------------------------------------------------------
    // Awake
    // -------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer) _originalColor = spriteRenderer.color;

        _ghostBallLayer = LayerMask.NameToLayer(ballLayerName);
    }

    private void Start()
    {
        GameStateManager.Source.OnGamePaused += OnGamePaused;
        GameStateManager.Source.OnGameUnpaused += OnGameUnpaused;
    }

    private void OnDestroy()
    {
        GameStateManager.Source.OnGamePaused -= OnGamePaused;
        GameStateManager.Source.OnGameUnpaused -= OnGameUnpaused;
    }

    // -------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------
    void Update()
    {
        if (_isPaused) return;

        if (IsPossessed && Owner && Owner.ballAnchor)
        {
            Vector3 target = Owner.ballAnchor.position;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * stickSmoothing);
            rb.linearVelocity = Vector2.zero;
        }

        if (_passGhostEndsAt > 0f && Time.time >= _passGhostEndsAt)
            EndPassGhost();

        if ((_ghostApplied || _ghostSolidDisabled || _ghostIgnoredEnemyColliders.Count > 0) &&
            _ghostActiveUntil > 0f && Time.time >= _ghostActiveUntil)
        {
            DeactivateGhost();
        }

        _ = IsStoneActive();
    }

    // -------------------------------------------------------------------
    // Trigger Events
    // -------------------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isPaused) return;
        if (TryPickupSmart(other)) return;

        if (shockwaveArmed && !shockwaveRunning)
        {
            if (ignoreOwner && Owner && other.GetComponentInParent<PlayerController>() == Owner)
                return;

            StartCoroutine(ShockwaveRoutine());
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if(_isPaused) return;
        TryPickupSmart(other);
    }

    // -------------------------------------------------------------------
    // SMART PICKUP V3 SYSTEM (FULL)
    // -------------------------------------------------------------------
    private bool TryPickupSmart(Collider2D col)
    {
        if (IsPossessed) return false;

        var pc = col.GetComponentInParent<PlayerController>();
        if (!pc || !pc.ballAnchor) return false;

        // Bloqueo tras pase
        if (Time.time < pickupBlockUntil && pc != _intendedReceiver)
            return false;

        // Evitar causas dobles
        if (Time.time < lastPickupTime + pickupCooldown)
            return false;

        Vector2 ballPos = transform.position;
        Vector2 anchorPos = pc.ballAnchor.position;

        // Radio dinámico
        float dynamicRadius = pickupRadius;
        if (ContactNearWall(ballPos))
            dynamicRadius += pickupWallBuffer;

        // IA auto “gira” hacia el balón para facilitar recepción
        if (pc != PlayerController.CurrentControlled)
            AutoFaceForPickup(pc, ballPos);

        float distSqr = (ballPos - anchorPos).sqrMagnitude;

        if (distSqr <= dynamicRadius * dynamicRadius)
        {
            if (!allowBackPickup)
            {
                Vector2 toBall = (ballPos - (Vector2)pc.transform.position).normalized;
                Vector2 facing = pc.transform.up.normalized;

                if (Vector2.Dot(facing, toBall) < -0.45f)
                    return false;
            }

            Take(pc);
            lastPickupTime = Time.time;
            return true;
        }

        return false;
    }

    private bool ContactNearWall(Vector2 ballPos)
    {
        var hits = Physics2D.OverlapCircleAll(ballPos, 0.23f);
        foreach (var h in hits)
        {
            if (!h) continue;

            if (h.CompareTag("Limit")) return true;

            int limitLayer = LayerMask.NameToLayer("Limit");
            if (limitLayer >= 0 && h.gameObject.layer == limitLayer)
                return true;
        }
        return false;
    }

    private void AutoFaceForPickup(PlayerController pc, Vector2 ballPos)
    {
        Vector2 dir = (ballPos - (Vector2)pc.transform.position).normalized;

        if (Mathf.Abs(dir.x) > 0.1f)
        {
            var scale = pc.transform.localScale;
            float targetX = Mathf.Sign(dir.x) * Mathf.Abs(scale.x);
            pc.transform.localScale = Vector3.Lerp(scale,
                new Vector3(targetX, scale.y, scale.z),
                Time.deltaTime * 9f);
        }
    }

    // -------------------------------------------------------------------
    // TAKE / DROP / PASS / SHOOT
    // -------------------------------------------------------------------
    public void Take(PlayerController newOwner)
    {
        _lastLinearVelocityBeforeCatch = rb.linearVelocity;

        Owner = newOwner;
        SetPossessedPhysics(true);
        rb.linearVelocity = Vector2.zero;

        EndPassGhost();
        _intendedReceiver = null;

        TryApplyStoneKnockbackOnCatch(newOwner);

        OnOwnerChanged?.Invoke(Owner);
    }

    public void Drop()
    {
        if (!IsPossessed) return;

        Owner = null;
        SetPossessedPhysics(false);

        EndPassGhost();
        _intendedReceiver = null;

        OnOwnerChanged?.Invoke(Owner);

        pickupBlockUntil = Time.time + pickupBlockSeconds;
        _lastLaunch = LastLaunch.None;
    }

    public void Pass(Vector2 dir, PlayerController passer = null)
    {
        if (!IsPossessed) return;

        Owner = null;
        SetPossessedPhysics(false);

        float v = passSpeed;
        if (IsStoneActive()) v *= _stonePassMul;

        rb.linearVelocity = dir.normalized * v;

        OnOwnerChanged?.Invoke(Owner);

        pickupBlockUntil = Time.time + pickupBlockSeconds;
        _intendedReceiver = null;

        _lastLaunch = LastLaunch.Pass;

        if (passer) BeginPassGhost(passGhostMin, passer);
        else EndPassGhost();
    }

    public void PassTo(Vector2 targetPos, PlayerController intendedReceiver, PlayerController passer)
    {
        if (!IsPossessed) return;

        Owner = null;
        SetPossessedPhysics(false);

        Vector2 toTarget = targetPos - (Vector2)transform.position;
        Vector2 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;

        float v = passSpeed;
        if (IsStoneActive()) v *= _stonePassMul;

        rb.linearVelocity = dir * v;

        OnOwnerChanged?.Invoke(Owner);

        pickupBlockUntil = Time.time + pickupBlockSeconds;

        _intendedReceiver = intendedReceiver;

        float travelTime = (v > 0f) ? (toTarget.magnitude / v) : 0f;
        float ghostDuration = Mathf.Clamp(travelTime + Mathf.Max(0.05f, passGhostExtraBuffer),
                                          passGhostMin, passGhostMax);

        _lastLaunch = LastLaunch.Pass;

        BeginPassGhost(ghostDuration, intendedReceiver, passer);
    }

    public void Shoot(Vector2 dir, float power01 = 1f)
    {
        if (!IsPossessed) return;

        Owner = null;
        SetPossessedPhysics(false);

        float baseV = Mathf.Lerp(shotSpeed * 0.7f, shotSpeed * 1.3f, Mathf.Clamp01(power01));
        if (IsStoneActive()) baseV *= _stoneShotMul;

        rb.linearVelocity = dir.normalized * baseV;

        OnOwnerChanged?.Invoke(Owner);
        pickupBlockUntil = Time.time + pickupBlockSeconds;

        EndPassGhost();
        _intendedReceiver = null;

        _lastLaunch = LastLaunch.Shoot;
    }

    public void ResetToPosition(Vector3 pos)
    {
        Owner = null;
        transform.position = pos;

        rb.linearVelocity = Vector2.zero;
        SetPossessedPhysics(false);

        EndPassGhost();
        _intendedReceiver = null;

        OnOwnerChanged?.Invoke(Owner);

        pickupBlockUntil = 0f;
        _lastLaunch = LastLaunch.None;
    }

    // -------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------
    private void SetPossessedPhysics(bool possessed)
    {
        if (triggerCollider && !triggerCollider.isTrigger)
            triggerCollider.isTrigger = true;

        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = possessed ?
                RigidbodyType2D.Kinematic :
                RigidbodyType2D.Dynamic;
        }

        if (solidCollider)
            solidCollider.enabled = !possessed;
    }

    private void BeginPassGhost(float duration, params PlayerController[] players)
    {
        EndPassGhost();
        if (!solidCollider) return;

        foreach (var p in players)
        {
            if (!p) continue;

            foreach (var c in p.GetComponentsInChildren<Collider2D>())
            {
                if (!c || !c.enabled) continue;
                Physics2D.IgnoreCollision(solidCollider, c, true);
                _temporarilyIgnored.Add(c);
            }
        }

        _passGhostEndsAt = Time.time + Mathf.Max(passGhostMin, duration);
    }

    private void EndPassGhost()
    {
        foreach (var c in _temporarilyIgnored)
        {
            if (!c) continue;
            Physics2D.IgnoreCollision(solidCollider, c, false);
        }

        _temporarilyIgnored.Clear();
        _passGhostEndsAt = -1f;
    }

    // -------------------------------------------------------------------
    // GHOST BALL
    // -------------------------------------------------------------------
    public void ActivateGhost(float seconds, string enemyLayerName = null)
    {
        if (seconds <= 0f) return;

        int enemyLayer = LayerMask.NameToLayer(
            string.IsNullOrEmpty(enemyLayerName) ? defaultEnemyLayerName : enemyLayerName
        );

        if (_ghostBallLayer >= 0 && enemyLayer >= 0)
        {
            if (!_ghostApplied)
            {
                Physics2D.IgnoreLayerCollision(_ghostBallLayer, enemyLayer, true);
                _ghostApplied = true;
                _ghostEnemyLayer = enemyLayer;
            }
        }

        if (ghostIgnorePerCollider)
            ApplyGhostIgnorePerCollider();

        if (spriteRenderer)
        {
            var c = _originalColor;
            c.a = ghostAlpha;
            spriteRenderer.color = c;
        }

        float until = Time.time + seconds;
        _ghostActiveUntil = Mathf.Max(_ghostActiveUntil, until);
    }

    public void DeactivateGhost()
    {
        if (_ghostApplied && _ghostBallLayer >= 0 && _ghostEnemyLayer >= 0)
            Physics2D.IgnoreLayerCollision(_ghostBallLayer, _ghostEnemyLayer, false);

        _ghostApplied = false;
        _ghostActiveUntil = -1f;

        RevertGhostIgnorePerCollider();

        if (spriteRenderer)
            spriteRenderer.color = _originalColor;
    }

    private void ApplyGhostIgnorePerCollider()
    {
        RevertGhostIgnorePerCollider();

        foreach (var e in GameObject.FindGameObjectsWithTag(enemyTag))
        {
            foreach (var ec in e.GetComponentsInChildren<Collider2D>())
            {
                Physics2D.IgnoreCollision(solidCollider, ec, true);
                _ghostIgnoredEnemyColliders.Add(ec);
            }
        }
    }

    private void RevertGhostIgnorePerCollider()
    {
        foreach (var ec in _ghostIgnoredEnemyColliders)
        {
            if (!ec) continue;
            Physics2D.IgnoreCollision(solidCollider, ec, false);
        }
        _ghostIgnoredEnemyColliders.Clear();
    }

    // -------------------------------------------------------------------
    // STONE BALL
    // -------------------------------------------------------------------
    private bool IsStoneActive()
    {
        if (_mode != BallMode.Stone) return false;

        if (_stoneUntil > 0f && Time.time > _stoneUntil)
        {
            DeactivateStoneBall();
            return false;
        }
        return true;
    }
    public void ActivateStoneBall(float duration, float shotMul, float passMul, float receiverImpulse)
    {
        // Cambiar modo
        _mode = BallMode.Stone;

        // Aplicar multiplicadores
        _stoneShotMul = shotMul;
        _stonePassMul = passMul;
        _stoneReceiverKnockbackImpulse = receiverImpulse;

        // Tiempo de duración
        _stoneUntil = Time.time + duration;

        // Cambiar color visual
        if (spriteRenderer)
            spriteRenderer.color = stoneColor;
    }

    private void DeactivateStoneBall()
    {
        _mode = BallMode.Normal;
        if (spriteRenderer) spriteRenderer.color = normalColor;
    }

    private void TryApplyStoneKnockbackOnCatch(PlayerController receiver)
    {
        if (!IsStoneActive()) return;
        if (_lastLaunch != LastLaunch.Pass) return;

        var recvRb = receiver.GetComponent<Rigidbody2D>();
        if (!recvRb) return;

        Vector2 arrival = _lastLinearVelocityBeforeCatch;
        if (arrival.sqrMagnitude < 0.0001f) return;

        Vector2 dir = arrival.normalized;

        recvRb.AddForce(dir * _stoneReceiverKnockbackImpulse, ForceMode2D.Impulse);
        recvRb.linearVelocity += dir * (_stoneReceiverKnockbackImpulse / Mathf.Max(0.5f, recvRb.mass));

        _lastLaunch = LastLaunch.None;
    }

    // -------------------------------------------------------------------
    // SHOCKWAVE
    // -------------------------------------------------------------------
    private IEnumerator ShockwaveRoutine()
    {
        shockwaveArmed = false;
        shockwaveRunning = true;

        _pushedIds.Clear();
        float elapsed = 0f;
        float prevR = innerRadius;

        while (elapsed < shockwaveDuration)
        {
            if (_isPaused)
            {
                yield return new WaitUntil(() => !_isPaused);
                // Recalcular el tiempo restante después de pausa
                elapsed += Time.deltaTime;
                continue;
            }

            float k = elapsed / shockwaveDuration;
            float currR = Mathf.Lerp(innerRadius, outerRadius, k);

            ApplyShockwaveRing(prevR, currR);
            prevR = currR;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!_isPaused)
        {
            ApplyShockwaveRing(prevR, outerRadius);
        }

        shockwaveRunning = false;
        _currentShockwaveCoroutine = null;
    }

    #region PAUSE LOGIC
    private void OnGamePaused()
    {
        _isPaused = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (_currentShockwaveCoroutine != null)
        {
            StopCoroutine(_currentShockwaveCoroutine);
            _currentShockwaveCoroutine = null;
            shockwaveRunning = false;
        }
    }

    private void OnGameUnpaused()
    {
        _isPaused = false;
    }
    #endregion

    private void ApplyShockwaveRing(float r0, float r1)
    {
        Vector2 center = transform.position;

        Collider2D[] hits =
            (affectMask.value != 0)
            ? Physics2D.OverlapCircleAll(center, r1, affectMask)
            : Physics2D.OverlapCircleAll(center, r1);

        foreach (var h in hits)
        {
            var pc = h.GetComponentInParent<PlayerController>();
            if (!pc) continue;

            if (ignoreOwner && Owner && pc == Owner) continue;

            int id = pc.gameObject.GetInstanceID();
            if (_pushedIds.Contains(id)) continue;

            var rbPlayer = pc.GetComponent<Rigidbody2D>();
            if (!rbPlayer) continue;

            Vector2 toPlayer = rbPlayer.worldCenterOfMass - center;
            float dist = toPlayer.magnitude;
            if (dist < r0 || dist > r1) continue;

            Vector2 dir = toPlayer.normalized;
            float t = Mathf.Clamp01(dist / outerRadius);
            float impulse = Mathf.Lerp(impulseMax, impulseMin, t);

            rbPlayer.AddForce(dir * impulse, ForceMode2D.Impulse);

            _pushedIds.Add(id);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius + pickupWallBuffer);
    }
#endif
}
