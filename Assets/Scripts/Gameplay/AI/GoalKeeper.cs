using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class GoalkeeperAI : MonoBehaviour
{
    [Header("Movimiento Lateral")]
    [Tooltip("Velocidad de movimiento del portero")]
    public float moveSpeed = 3.5f;

    [Tooltip("Límites de movimiento del portero (en coordenadas world Y)")]
    public Vector2 movementBounds = new Vector2(-3f, 3f);

    [Tooltip("Margen de tolerancia al llegar a la posición objetivo (evita jitter)")]
    public float positionTolerance = 0.2f;

    [Header("Atajadas / Rebotes")]
    [Tooltip("Multiplicador de la velocidad actual del balón al rebotar")]
    public float bounceSpeedMultiplier = 1.1f;

    [Tooltip("Velocidad mínima a la que saldrá el balón tras el rebote")]
    public float minBounceSpeed = 6f;

    [Tooltip("Grados máximos de variación aleatoria en la dirección del rebote")]
    public float randomAngleVariation = 10f;

    [Tooltip("Impulso extra hacia fuera del portero (para asegurar que salga del área)")]
    public float extraImpulse = 0.5f;

    private BallController ball;
    private Rigidbody2D rb;
    private bool initialized = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb)
        {
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        initialized = true;
    }

    void FixedUpdate()
    {
        if (!initialized || !rb) return;

        ball = BallController.Instance;
        if (!ball)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Si estamos en cuenta regresiva, no moverse
        if (MatchTimer.CountdownActive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Moverse para alinearse con el balón sobre el eje Y
        var ballY = ball.transform.position.y;
        var myY = transform.position.y;

        var diff = ballY - myY;

        if (Mathf.Abs(diff) < positionTolerance)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        var targetVelY = Mathf.Sign(diff) * moveSpeed;

        // Limitar movimiento a los bounds
        if (targetVelY < 0 && myY <= movementBounds.x) targetVelY = 0;
        if (targetVelY > 0 && myY >= movementBounds.y) targetVelY = 0;

        rb.linearVelocity = new Vector2(0f, targetVelY);
    }

    /// <summary>
    /// Rebote del balón al chocar con el portero.
    /// Asegúrate de que el portero tenga un Collider2D NO trigger
    /// y el balón un Rigidbody2D + Collider2D.
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        var ballController = collision.gameObject.GetComponent<BallController>();
        if (ballController == null) return;

        var ballRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (ballRb == null) return;

        // Dirección desde el portero hacia el balón (para que salga hacia fuera)
        Vector2 dirFromKeeper = (ballRb.position - (Vector2)transform.position).normalized;
        if (dirFromKeeper == Vector2.zero)
        {
            // Fallback: empujar en X negativa o positiva según donde esté la portería
            dirFromKeeper = Vector2.right;
        }

        // Tomamos la magnitud actual de la velocidad del balón
        float currentSpeed = ballRb.linearVelocity.magnitude;
        float targetSpeed = Mathf.Max(currentSpeed * bounceSpeedMultiplier, minBounceSpeed);

        // Variación aleatoria de ángulo para que no sea un rebote robótico
        float angleOffset = Random.Range(-randomAngleVariation, randomAngleVariation);
        dirFromKeeper = Quaternion.Euler(0f, 0f, angleOffset) * dirFromKeeper;

        // Aplicar nueva velocidad
        Vector2 newVelocity = dirFromKeeper.normalized * targetSpeed;

        // Pequeño impulso extra para sacarlo del área
        newVelocity += dirFromKeeper.normalized * extraImpulse;

        ballRb.linearVelocity = newVelocity;
    }

    // Visualización en el editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        var bottomBound = new Vector3(transform.position.x, movementBounds.x, 0f);
        var topBound = new Vector3(transform.position.x, movementBounds.y, 0f);

        Gizmos.DrawLine(bottomBound + Vector3.right * 2f, bottomBound - Vector3.right * 2f);
        Gizmos.DrawLine(topBound + Vector3.right * 2f, topBound - Vector3.right * 2f);
        Gizmos.DrawLine(bottomBound, topBound);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position - Vector3.up);
    }
}
