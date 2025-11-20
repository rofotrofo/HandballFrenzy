using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class GoalkeeperAI : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Poste izquierdo del arco (opcional, para límites automáticos)")]
    public Transform leftPost;
    
    [Tooltip("Poste derecho del arco (opcional, para límites automáticos)")]
    public Transform rightPost;

    [Header("Movimiento Lateral")]
    [Tooltip("Velocidad de movimiento del portero")]
    public float moveSpeed = 3.5f;
    
    [Tooltip("Límites izquierdo y derecho del área del portero (en coordenadas world X)")]
    public Vector2 movementBounds = new Vector2(-3f, 3f);
    
    [Tooltip("Margen de tolerancia al llegar a la posición objetivo (evita jitter)")]
    public float positionTolerance = 0.2f;

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
        
        var ball = BallController.Instance;
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

        // Obtener posición X del balón
        float ballY = ball.transform.position.y;
        float myY = transform.position.y;
        
        // Calcular diferencia
        float diff = ballY - myY;
        
        // Si ya estamos cerca del balón, no moverse
        if (Mathf.Abs(diff) < positionTolerance)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        
        // Calcular velocidad objetivo (solo en X)
        float targetVelY = Mathf.Sign(diff) * moveSpeed;
        
        // Limitar movimiento a los bounds
        if (targetVelY < 0 && myY <= movementBounds.x) targetVelY = 0;
        if (targetVelY > 0 && myY >= movementBounds.y) targetVelY = 0;
        
        // Aplicar velocidad (mantener Y en 0)
        rb.linearVelocity = new Vector2(0f, targetVelY);
    }

    // Visualización en el editor
    void OnDrawGizmosSelected()
    {
        // Dibujar límites de movimiento
        Gizmos.color = Color.yellow;
        Vector3 leftBound = new Vector3(transform.position.x, movementBounds.x, 0f);
        Vector3 rightBound = new Vector3(transform.position.x, movementBounds.y, 0f);
        
        Gizmos.DrawLine(leftBound + Vector3.right * 2f, leftBound - Vector3.right * 2f);
        Gizmos.DrawLine(rightBound + Vector3.right * 2f, rightBound - Vector3.right * 2f);
        Gizmos.DrawLine(leftBound, rightBound);
        
        // Línea del portero
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position - Vector3.up);
    }
}