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

    [Header("Pickup cooldown (s)")]
    [SerializeField, Min(0f)] private float pickupBlockSeconds = 0.2f;

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
    [SerializeField] private Color ghostColor = new Color(1f, 0f, 0f); // (no se usa directamente, mantenido por compatibilidad)

    // -------- Ghost Ball ----------
    [Header("Ghost Ball (global/per-collider)")]
    [Tooltip("Nombre de la capa de la pelota en el proyecto")]
    public string ballLayerName = "Ball";

    [Tooltip("Capa de enemigos por defecto a ignorar si el pickup no especifica")]
    public string defaultEnemyLayerName = "Enemy";

    [Tooltip("Tag de los enemigos (para ignorar por collider si se activa la opción)")]
    public string enemyTag = "Enemy";

    [Tooltip("Ignorar por collider además de por capa (más robusto si hay hijos con capas distintas)")]
    public bool ghostIgnorePerCollider = true;

    [Tooltip("Apaga el collider sólido mientras esté activo el ghost")]
    public bool disableSolidColliderDuringGhost = true;

    [Tooltip("Alpha cuando está activa la bola fantasma (feedback)")]
    [Range(0f, 1f)] public float ghostAlpha = 0.45f;

    private float _ghostActiveUntil = -1f;
    private int _ghostBallLayer = -1;
    private int _ghostEnemyLayer = -1;
    private bool _ghostApplied = false;
    private Color _originalColor;

    // Control del collider sólido durante ghost
    private bool _ghostSolidDisabled = false;
    private bool _solidPrevEnabled = true;

    // Registro de ignores por collider para revertir
    private readonly List<Collider2D> _ghostIgnoredEnemyColliders = new();
    // ---------------------------------------------------------------

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

        // Cache de capas para Ghost Ball
        _ghostBallLayer = LayerMask.NameToLayer(ballLayerName);
        if (_ghostBallLayer < 0)
            Debug.LogWarning($"[BallController] La capa '{ballLayerName}' no existe. Revisa Project Settings > Tags & Layers.");
    }

    void Update()
    {
        if (IsPossessed && Owner && Owner.ballAnchor)
        {
            Vector3 target = Owner.ballAnchor.position;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * stickSmoothing);
            rb.linearVelocity = Vector2.zero;
        }

        if (_passGhostEndsAt > 0f && Time.time >= _passGhostEndsAt)
            EndPassGhost();

        // Expirar Ghost Ball
        if ((_ghostApplied || _ghostSolidDisabled || _ghostIgnoredEnemyColliders.Count > 0) &&
            _ghostActiveUntil > 0f && Time.time >= _ghostActiveUntil)
        {
            DeactivateGhost();
        }

        _ = IsStoneActive();
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // PICKUP NORMAL
        if (!IsPossessed && other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (!pc) return;
            if (Time.time < pickupBlockUntil && pc != _intendedReceiver) return;
            Take(pc);
            return;
        }

        // SHOCKWAVE TRIGGER
        if (shockwaveArmed && !shockwaveRunning)
        {
            // evita detonar con el propio dueño
            if (ignoreOwner && Owner && other.GetComponentInParent<PlayerController>() == Owner)
                return;

            StartCoroutine(ShockwaveRoutine());
        }
    }

    // ----------- API -----------
    public void Take(PlayerController newOwner)
    {
        _lastLinearVelocityBeforeCatch = rb ? rb.linearVelocity : Vector2.zero;
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
        if (passer) BeginPassGhost(duration: passGhostMin, players: passer);
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
        float travelTime = (v > 0f) ? (toTarget.magnitude / v) : 0.0f;
        float ghostDuration = Mathf.Clamp(travelTime + Mathf.Max(0.05f, passGhostExtraBuffer),
                                          passGhostMin, passGhostMax);
        _lastLaunch = LastLaunch.Pass;
        if (intendedReceiver || passer)
            BeginPassGhost(ghostDuration, intendedReceiver, passer);
        else
            EndPassGhost();
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

    // -------- Helpers ----------
    private void SetPossessedPhysics(bool possessed)
    {
        if (triggerCollider && !triggerCollider.isTrigger)
            triggerCollider.isTrigger = true;
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = possessed ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        }
        if (solidCollider)
        {
            // Si Ghost apagó el sólido, no lo enciendas por error
            if (_ghostSolidDisabled)
                solidCollider.enabled = false;
            else
                solidCollider.enabled = !possessed;
        }
    }

    private void BeginPassGhost(float duration, params PlayerController[] players)
    {
        EndPassGhost();
        if (!solidCollider) { _passGhostEndsAt = -1f; return; }
        foreach (var p in players)
        {
            if (!p) continue;
            var cols = p.GetComponentsInChildren<Collider2D>();
            foreach (var c in cols)
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
        if (!solidCollider)
        {
            _temporarilyIgnored.Clear();
            _passGhostEndsAt = -1f;
            return;
        }
        foreach (var c in _temporarilyIgnored)
        {
            if (!c) continue;
            Physics2D.IgnoreCollision(solidCollider, c, false);
        }
        _temporarilyIgnored.Clear();
        _passGhostEndsAt = -1f;
    }

    // ---------- API Ghost Ball ----------
    /// <summary>
    /// Activa la bola fantasma (ignora colisiones con enemigos y puede apagar el sólido).
    /// </summary>
    public void ActivateGhost(float seconds, string enemyLayerName = null)
    {
        if (seconds <= 0f) return;

        // 1) Intento por capas (global)
        if (_ghostBallLayer < 0)
            _ghostBallLayer = LayerMask.NameToLayer(ballLayerName);

        string layerToUse = string.IsNullOrEmpty(enemyLayerName) ? defaultEnemyLayerName : enemyLayerName;
        int enemyLayer = LayerMask.NameToLayer(layerToUse);

        if (_ghostBallLayer >= 0 && enemyLayer >= 0)
        {
            // Si ya estaba aplicado contra otra capa, revierte la anterior
            if (_ghostApplied && _ghostEnemyLayer >= 0 && (_ghostEnemyLayer != enemyLayer))
            {
                Physics2D.IgnoreLayerCollision(_ghostBallLayer, _ghostEnemyLayer, false);
                _ghostApplied = false;
            }

            if (!_ghostApplied)
            {
                Physics2D.IgnoreLayerCollision(_ghostBallLayer, enemyLayer, true);
                _ghostApplied = true;
                _ghostEnemyLayer = enemyLayer;
            }
        }
        else
        {
            Debug.LogWarning($"[BallController] GhostBall: capas no válidas. Ball='{ballLayerName}'({_ghostBallLayer}) Enemy='{layerToUse}'({enemyLayer}). Se usará ignore por collider si está activo.");
        }

        // 2) Opcional (recomendado): ignorar por collider a todo lo que tenga tag Enemy
        if (ghostIgnorePerCollider)
            ApplyGhostIgnorePerCollider();

        // 3) Feedback visual
        if (spriteRenderer)
        {
            _originalColor = spriteRenderer.color;
            var c = _originalColor; c.a = ghostAlpha;
            spriteRenderer.color = c;
        }

        // 4) Apagar collider sólido mientras dure el ghost
        if (disableSolidColliderDuringGhost && solidCollider && !_ghostSolidDisabled)
        {
            _solidPrevEnabled = solidCollider.enabled;
            solidCollider.enabled = false;
            _ghostSolidDisabled = true;
            // De paso, desactiva PassGhost por-collider si estuviera activo
            EndPassGhost();
        }

        // 5) Extiende duración
        float until = Time.time + seconds;
        _ghostActiveUntil = Mathf.Max(_ghostActiveUntil, until);
    }

    /// <summary>
    /// Desactiva la bola fantasma y restaura colisiones/visual/collider sólido.
    /// </summary>
    public void DeactivateGhost()
    {
        // Revertir capa global
        if (_ghostApplied && _ghostBallLayer >= 0 && _ghostEnemyLayer >= 0)
            Physics2D.IgnoreLayerCollision(_ghostBallLayer, _ghostEnemyLayer, false);

        _ghostApplied = false;
        _ghostActiveUntil = -1f;

        // Revertir ignores por collider
        RevertGhostIgnorePerCollider();

        // Restaurar collider sólido si lo apagamos por Ghost
        if (_ghostSolidDisabled && solidCollider)
        {
            solidCollider.enabled = _solidPrevEnabled;
            _ghostSolidDisabled = false;
        }

        // Restaurar visual
        if (spriteRenderer)
            spriteRenderer.color = _originalColor;
    }

    private void ApplyGhostIgnorePerCollider()
    {
        // Limpia registro previo si re-aplican durante el efecto
        RevertGhostIgnorePerCollider();
        _ghostIgnoredEnemyColliders.Clear();

        var enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        foreach (var e in enemies)
        {
            if (!e || !e.activeInHierarchy) continue;
            var enemyCols = e.GetComponentsInChildren<Collider2D>(includeInactive: false);
            foreach (var ec in enemyCols)
            {
                if (!ec || !ec.enabled) continue;
                if (solidCollider) Physics2D.IgnoreCollision(solidCollider, ec, true);
                if (triggerCollider) Physics2D.IgnoreCollision(triggerCollider, ec, true);
                _ghostIgnoredEnemyColliders.Add(ec);
            }
        }
    }

    private void RevertGhostIgnorePerCollider()
    {
        if (_ghostIgnoredEnemyColliders.Count == 0) return;

        foreach (var ec in _ghostIgnoredEnemyColliders)
        {
            if (!ec) continue;
            if (solidCollider) Physics2D.IgnoreCollision(solidCollider, ec, false);
            if (triggerCollider) Physics2D.IgnoreCollision(triggerCollider, ec, false);
        }
        _ghostIgnoredEnemyColliders.Clear();
    }

    // ---------- Stone ----------
    public void ActivateStoneBall(float duration, float shotMul, float passMul, float receiverImpulse)
    {
        _mode = BallMode.Stone;
        _stoneShotMul = shotMul;
        _stonePassMul = passMul;
        _stoneReceiverKnockbackImpulse = receiverImpulse;
        _stoneUntil = Time.time + Mathf.Max(0.01f, duration);
        if (spriteRenderer) spriteRenderer.color = stoneColor;
    }

    private void DeactivateStoneBall()
    {
        _mode = BallMode.Normal;
        _stoneUntil = -1f;
        if (spriteRenderer) spriteRenderer.color = normalColor;
    }

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

    private void TryApplyStoneKnockbackOnCatch(PlayerController receiver)
    {
        if (!IsStoneActive()) return;
        if (_lastLaunch != LastLaunch.Pass) return;
        if (!receiver) return;
        var recvRb = receiver.GetComponent<Rigidbody2D>();
        if (!recvRb) return;
        Vector2 arrival = _lastLinearVelocityBeforeCatch;
        if (arrival.sqrMagnitude < 0.0001f) return;
        Vector2 dir = arrival.normalized;
        recvRb.AddForce(dir * _stoneReceiverKnockbackImpulse, ForceMode2D.Impulse);
        recvRb.linearVelocity += dir * (_stoneReceiverKnockbackImpulse / Mathf.Max(0.5f, recvRb.mass));
        _lastLaunch = LastLaunch.None;
        _lastLinearVelocityBeforeCatch = Vector2.zero;
    }

    // ---------- Shockwave ----------
    public void ActivateShockwave(float inner, float outer, float dur, float impMax, float impMin, LayerMask mask)
    {
        shockwaveArmed = true;
        innerRadius = inner;
        outerRadius = outer;
        shockwaveDuration = dur;
        impulseMax = impMax;
        impulseMin = impMin;
        affectMask = mask;
    }

    private IEnumerator ShockwaveRoutine()
    {
        shockwaveArmed = false;
        shockwaveRunning = true;
        _pushedIds.Clear();

        float elapsed = 0f;
        float prevR = innerRadius;

        while (elapsed < shockwaveDuration)
        {
            float k = elapsed / shockwaveDuration;
            float currR = Mathf.Lerp(innerRadius, outerRadius, k);
            ApplyShockwaveRing(prevR, currR);
            prevR = currR;
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyShockwaveRing(prevR, outerRadius);
        shockwaveRunning = false;
    }

    private void ApplyShockwaveRing(float r0, float r1)
    {
        Vector2 center = transform.position;
        if (r1 <= 0f) return;
        Collider2D[] hits = (affectMask.value != 0)
            ? Physics2D.OverlapCircleAll(center, r1, affectMask)
            : Physics2D.OverlapCircleAll(center, r1);

        foreach (var h in hits)
        {
            if (!h || !h.gameObject.activeInHierarchy) continue;
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
            rbPlayer.linearVelocity += dir * (impulse / Mathf.Max(0.5f, rbPlayer.mass));

            _pushedIds.Add(id);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (shockwaveArmed || shockwaveRunning)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, innerRadius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, outerRadius);
        }
    }
#endif
}
