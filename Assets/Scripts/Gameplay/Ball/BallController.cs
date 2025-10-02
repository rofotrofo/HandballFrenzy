using UnityEngine;
using System;

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

    private Rigidbody2D rb;

    public PlayerController Owner { get; private set; }
    public event Action<PlayerController> OnOwnerChanged;

    private float pickupBlockUntil;
    private bool IsPossessed => Owner != null;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Validaciones básicas
        if (!solidCollider) Debug.LogWarning("[BallController] Falta asignar solidCollider.");
        if (!triggerCollider) Debug.LogWarning("[BallController] Falta asignar triggerCollider.");
    }

    void Update()
    {
        // Cuando está poseída, seguimos el anchor suavemente y NO dejamos velocidad residual
        if (IsPossessed && Owner && Owner.ballAnchor)
        {
            Vector3 target = Owner.ballAnchor.position;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * stickSmoothing);
            rb.linearVelocity = Vector2.zero;
        }
    }

    // Este callback debe venir del collider en capa BallTrigger (Trigger=ON)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < pickupBlockUntil) return;
        if (IsPossessed) return;

        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc) Take(pc);
        }
    }

    // ----------------- API pública -----------------
    public void Take(PlayerController newOwner)
    {
        Owner = newOwner;

        // Desactivamos física "sólida" mientras está pegada al jugador
        SetPossessedPhysics(true);

        rb.linearVelocity = Vector2.zero;
        OnOwnerChanged?.Invoke(Owner);
    }

    public void Drop()
    {
        if (!IsPossessed) return;
        Owner = null;

        // Reactivar física normal
        SetPossessedPhysics(false);

        OnOwnerChanged?.Invoke(Owner);
        pickupBlockUntil = Time.time + pickupBlockSeconds;
    }

    public void Pass(Vector2 dir)
    {
        if (!IsPossessed) return;
        Owner = null;

        // Reactivar física y lanzar
        SetPossessedPhysics(false);
        rb.linearVelocity = dir.normalized * passSpeed;

        OnOwnerChanged?.Invoke(Owner);
        pickupBlockUntil = Time.time + pickupBlockSeconds;
    }

    public void Shoot(Vector2 dir, float power01 = 1f)
    {
        if (!IsPossessed) return;
        Owner = null;

        SetPossessedPhysics(false);
        float v = Mathf.Lerp(shotSpeed * 0.7f, shotSpeed * 1.3f, Mathf.Clamp01(power01));
        rb.linearVelocity = dir.normalized * v;

        OnOwnerChanged?.Invoke(Owner);
        pickupBlockUntil = Time.time + pickupBlockSeconds;
    }

    public void ResetToPosition(Vector3 pos)
    {
        Owner = null;
        transform.position = pos;
        rb.linearVelocity = Vector2.zero;
        SetPossessedPhysics(false);
        OnOwnerChanged?.Invoke(Owner);
        pickupBlockUntil = 0f;
    }

    // ----------------- Helpers -----------------
    /// <summary>
    /// true: poseída (stick) → Kinematic + collider sólido OFF
    /// false: libre → Dynamic + collider sólido ON
    /// </summary>
    private void SetPossessedPhysics(bool possessed)
    {
        // El trigger SIEMPRE debe quedar ON para pickup/gol
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
}
