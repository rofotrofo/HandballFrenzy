using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class IceZone2D : MonoBehaviour
{
    [Header("Players (slip)")]
    [Tooltip("Multiplicador de velocidad base. Suele ser ~1.0 en hielo (no más lento, solo resbaloso)")]
    [Range(0.5f, 1.5f)] public float playerSpeedMultiplier = 1.0f;
    [Tooltip("Aceleración cuando hay input (u/s^2 aprox)")]
    public float slipAccel = 18f;
    [Tooltip("Desaceleración al soltar (u/s^2 aprox). Más bajo = se desliza más")]
    public float slipDecel = 3f;

    [Header("Ball (optional)")]
    [Tooltip("Factor por segundo para frenar la pelota. 1 = no frena; 0.98 = frena muy poco")]
    [Range(0.9f, 1f)] public float ballVelocityFactorPerSecond = 0.99f;

    Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col && !col.isTrigger) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (!pc) return;
            pc.surfaceMultiplier = playerSpeedMultiplier;
            pc.SetSlip(true, slipAccel, slipDecel);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (!pc) return;
            pc.surfaceMultiplier = 1f;
            pc.SetSlip(false); // vuelve a modo directo
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var ball = other.GetComponentInParent<BallController>();
        if (!ball) return;

        var rb = ball.GetComponent<Rigidbody2D>();
        if (!rb || rb.bodyType != RigidbodyType2D.Dynamic) return;

        float f = Mathf.Clamp(ballVelocityFactorPerSecond, 0.9f, 1f);
        float frameFactor = Mathf.Pow(f, Time.deltaTime);
        rb.linearVelocity *= frameFactor;
    }
}