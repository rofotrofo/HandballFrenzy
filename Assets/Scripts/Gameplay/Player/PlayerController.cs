using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float speed = 5f;

    [Header("Team")]
    public TeamId teamId = TeamId.Blue;

    [Header("Ball Anchor")]
    public Transform ballAnchor; // punto frente al jugador donde "se pega" la pelota

    [Header("Targets")]
    [Tooltip("Transform del centro (o punto) de la portería rival")]
    public Transform opponentGoal;              // tiro directo
    [Tooltip("Jugador al que quieres pasar por defecto (si está vacío, se usa passTargetTransform o el compañero más cercano)")]
    public PlayerController passTarget;         // receptor preferido
    [Tooltip("Transform alternativo para pase (por si prefieres asignar un Transform en lugar de PlayerController)")]
    public Transform passTargetTransform;       // alternativo si no asignas PlayerController

    [Header("Fallbacks")]
    [Tooltip("Si no hay passTarget ni passTargetTransform, elegir automáticamente el compañero más cercano")]
    public bool autoPickClosestTeammateIfNoTarget = true;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        TeamRegistry.Register(this);
    }

    void OnDestroy() => TeamRegistry.Unregister(this);

    public void SetMoveInput(Vector2 v) => moveInput = v;

    void FixedUpdate()
    {
        if (!rb || !rb.simulated) return;
        rb.linearVelocity = moveInput * speed;
    }

    // --------- Acciones ----------
    public void ActionPass()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        // 1) passTarget explícito
        if (passTarget)
        {
            Vector2 to = passTarget.ballAnchor ? (Vector2)passTarget.ballAnchor.position
                                               : (Vector2)passTarget.transform.position;
            ball.PassTo(to, intendedReceiver: passTarget, passer: this); // <<— receptor y pasador
            return;
        }

        // 2) passTargetTransform
        if (passTargetTransform)
        {
            Vector2 to = (Vector2)passTargetTransform.position;
            ball.PassTo(to, intendedReceiver: null, passer: this); // <<— solo pasador
            return;
        }

        // 3) auto pick compañero más cercano
        if (autoPickClosestTeammateIfNoTarget)
        {
            var mate = TeamRegistry.GetClosestTeammate(this);
            if (mate)
            {
                Vector2 to = mate.ballAnchor ? (Vector2)mate.ballAnchor.position
                                             : (Vector2)mate.transform.position;
                ball.PassTo(to, intendedReceiver: mate, passer: this); // <<— receptor y pasador
                return;
            }
        }

        // 4) fallback: hacia adelante (al menos ignora al pasador)
        ball.Pass(transform.up, passer: this);
    }

    public void ActionShoot()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        Vector2 dir = opponentGoal
            ? ((Vector2)opponentGoal.position - (Vector2)ball.transform.position).normalized
            : (Vector2)transform.up;

        ball.Shoot(dir, 1f);
    }

    public void ActionDrop()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        ball.Drop();
    }
}
