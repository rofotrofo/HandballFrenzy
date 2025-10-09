using UnityEngine;
using System;
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
    [Tooltip("Collider sólido (NO Trigger) en capa Ball, para rebotar con Arena")]
    public Collider2D solidCollider;
    [Tooltip("Collider Trigger en capa BallTrigger, para pickup y gol")]
    public Collider2D triggerCollider;

    [Header("Ghost pass")]
    [Tooltip("Margen extra que se suma al tiempo de viaje para el ghost (s)")]
    public float passGhostExtraBuffer = 0.15f;
    [Tooltip("Límite superior del tiempo de ghost (s)")]
    public float passGhostMax = 1.0f;
    [Tooltip("Límite inferior del tiempo de ghost (s)")]
    public float passGhostMin = 0.15f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color stoneColor = new Color(0.6f, 0.6f, 0.6f);

    
    //Stone Ball (Power-Up)
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
    // ========================================

    private Rigidbody2D rb;

    public PlayerController Owner { get; private set; }
    public event Action<PlayerController> OnOwnerChanged;

    private float pickupBlockUntil;
    private bool IsPossessed => Owner != null;

    // Ghost: colliders ignorados (pasador + receptor)
    private readonly List<Collider2D> _temporarilyIgnored = new();
    private float _passGhostEndsAt = -1f;

    // Quién debería recibir este pase (para permitir pickup aunque haya cooldown)
    private PlayerController _intendedReceiver = null;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (!solidCollider) Debug.LogWarning("[BallController] Falta asignar solidCollider.");
        if (!triggerCollider) Debug.LogWarning("[BallController] Falta asignar triggerCollider.");
        
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

    }

    void Update()
    {
        // Seguir al dueño
        if (IsPossessed && Owner && Owner.ballAnchor)
        {
            Vector3 target = Owner.ballAnchor.position;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * stickSmoothing);
            rb.linearVelocity = Vector2.zero;
        }

        // Expira ventana de ghost por tiempo
        if (_passGhostEndsAt > 0f && Time.time >= _passGhostEndsAt)
            EndPassGhost();

        // Expirar power-up stone si corresponde
        _ = IsStoneActive();
    }

    // Este callback debe venir del collider en capa BallTrigger (Trigger=ON)
    void OnTriggerEnter2D(Collider2D other)
    {
        // Permite pickup AUN CON COOLDOWN si es el receptor intencionado
        if (IsPossessed) return;

        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (!pc) return;

            // Si no ha pasado el cooldown pero es el receptor del pase, dejar tomar
            if (Time.time < pickupBlockUntil && pc != _intendedReceiver)
                return;

            Take(pc);
        }
    }

    // --------- API ---------
    public void Take(PlayerController newOwner)
    {
        // Guardar velocidad de llegada ANTES de anularla al pegarse
        _lastLinearVelocityBeforeCatch = rb ? rb.linearVelocity : Vector2.zero;

        Owner = newOwner;
        SetPossessedPhysics(true);
        rb.linearVelocity = Vector2.zero;

        // Al atraparla, cancelamos ghost y limpiamos receptor objetivo
        EndPassGhost();
        _intendedReceiver = null;

        // Aplicar knockback si corresponde (pase + stone activo)
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

    // Pase con dirección simple; puede ignorar al pasador si se provee
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

        _intendedReceiver = null; // sin receptor explícito
        _lastLaunch = LastLaunch.Pass;

        if (passer) BeginPassGhost(duration: passGhostMin, players: passer); else EndPassGhost();
    }

    /// <summary>
    /// Pase dirigido a una posición, ignorando colisiones con receptor y pasador.
    /// Ghost dura distancia/velocidad + buffer (clamp a [min, max]).
    /// </summary>
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

        // Configura receptor objetivo (para saltarnos cooldown si llega antes)
        _intendedReceiver = intendedReceiver;

        // Duración de ghost = tiempo de viaje + buffer, clamped
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

    // --------- Helpers ---------
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
            solidCollider.enabled = !possessed;
    }

    private void BeginPassGhost(float duration, params PlayerController[] players)
    {
        EndPassGhost(); // limpia por si había algo anterior
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

    private void BeginPassGhost(params PlayerController[] players)
    {
        BeginPassGhost(passGhostMin, players);
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

    // ======== Stone Ball: Activación / Expiración / Knockback ========

    public void ActivateStoneBall(float duration, float shotMul, float passMul, float receiverImpulse)
    {
        _mode = BallMode.Stone;
        _stoneShotMul = shotMul;
        _stonePassMul = passMul;
        _stoneReceiverKnockbackImpulse = receiverImpulse;
        _stoneUntil = Time.time + Mathf.Max(0.01f, duration);
        
        if (spriteRenderer)
            spriteRenderer.color = stoneColor;


        // TODO: activar VFX/material/partículas de la pelota “de piedra”
        // e.g., cambiar color, habilitar trail, etc.
    }

    private void DeactivateStoneBall()
    {
        _mode = BallMode.Normal;
        _stoneUntil = -1f;
        
        if (spriteRenderer)
            spriteRenderer.color = normalColor;


        // TODO: desactivar VFX/material/partículas
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
        // Solo si:
        // - Stone activo
        // - La pelota venía de un PASE
        // - Tenemos receptor y su Rigidbody2D
        if (!IsStoneActive()) return;
        if (_lastLaunch != LastLaunch.Pass) return;
        if (!receiver) return;

        var recvRb = receiver.GetComponent<Rigidbody2D>();
        if (!recvRb) return;

        Vector2 arrival = _lastLinearVelocityBeforeCatch;
        if (arrival.sqrMagnitude < 0.0001f) return;

        Vector2 dir = arrival.normalized;
        recvRb.AddForce(dir * _stoneReceiverKnockbackImpulse, ForceMode2D.Impulse);

        // Después de aplicar, reseteamos el último lanzamiento
        _lastLaunch = LastLaunch.None;
        _lastLinearVelocityBeforeCatch = Vector2.zero;
    }
}
