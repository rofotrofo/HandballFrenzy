using UnityEngine;

/// Pickup que activa la "Bola de Piedra" en la pelota.
[RequireComponent(typeof(Collider2D))]
public class PowerUpStonePickup : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Duración del power-up en segundos")]
    public float duration = 8f;

    [Tooltip("Multiplicador de velocidad de TIRO")]
    public float shotSpeedMultiplier = 1.6f;

    [Tooltip("Multiplicador de velocidad de PASE")]
    public float passSpeedMultiplier = 1.3f;

    [Tooltip("Impulso al receptor al atrapar un PASE")]
    public float receiverKnockbackImpulse = 8f;

    [Header("VFX (opcional)")]
    public GameObject pickupVfx;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        gameObject.tag = "PowerUp"; // para ocupar el spawnPoint
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Activa cuando la PELOTA toca el pickup (sea el collider de la bola o un hijo)
        var ball = other.GetComponentInParent<BallController>();
        if (!ball) ball = other.GetComponent<BallController>();
        if (!ball) return;

        ball.ActivateStoneBall(duration, shotSpeedMultiplier, passSpeedMultiplier, receiverKnockbackImpulse);

        if (pickupVfx) Instantiate(pickupVfx, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}