using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class PowerUpGhostBallPickup : MonoBehaviour
{
    [Header("Configuración del efecto")]
    [Tooltip("Duración del efecto de bola fantasma (segundos)")]
    public float ghostDuration = 3f;

    [Tooltip("Nombre de la capa normal de la pelota")]
    public string normalBallLayer = "Ball";

    [Tooltip("Nombre de la capa de enemigos que será ignorada")]
    public string enemyLayerName = "TeamB";

    [Tooltip("Color visual temporal de la bola fantasma")]
    public Color ghostColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("Efectos visuales")]
    public GameObject pickupVfx;
    public AudioClip pickupSfx;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (!CompareTag("PowerUp")) gameObject.tag = "PowerUp";
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var ball = other.GetComponentInParent<BallController>();
        if (!ball) return;

        ball.ActivateGhost(3f, "Enemy"); // ← 3 segundos ignorando la capa TeamB
        Destroy(gameObject);
    }


    private IEnumerator ActivateGhostBall(BallController ball, float duration)
    {
        var sprite = ball.GetComponentInChildren<SpriteRenderer>();
        var rb = ball.GetComponent<Rigidbody2D>();

        int ballLayer = LayerMask.NameToLayer(normalBallLayer);
        int enemyLayer = LayerMask.NameToLayer(enemyLayerName);

        // Cambiar color de la pelota (feedback visual)
        Color originalColor = sprite ? sprite.color : Color.white;
        if (sprite) sprite.color = ghostColor;

        // Ignorar colisión entre capa Ball y enemigos
        if (ballLayer >= 0 && enemyLayer >= 0)
            Physics2D.IgnoreLayerCollision(ballLayer, enemyLayer, true);

        // Esperar duración del efecto
        yield return new WaitForSeconds(duration);

        // Revertir cambios
        if (sprite) sprite.color = originalColor;
        if (ballLayer >= 0 && enemyLayer >= 0)
            Physics2D.IgnoreLayerCollision(ballLayer, enemyLayer, false);
    }
}
