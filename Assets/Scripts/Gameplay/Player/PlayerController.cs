using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float speed = 5f;

    [Header("Team")]
    public TeamId teamId = TeamId.Blue;

    [Header("Ball Anchor")]
    public Transform ballAnchor;

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

    public void ActionPass()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        var mate = TeamRegistry.GetClosestTeammate(this);
        Vector2 dir = mate ? (mate.ballAnchor.position - ball.transform.position).normalized
            : (Vector2)transform.up;
        ball.Pass(dir);
    }

    public void ActionShoot()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        Vector2 dir = transform.up;
        ball.Shoot(dir, 1f);
    }

    public void ActionDrop()
    {
        var ball = BallController.Instance;
        if (!ball || ball.Owner != this) return;

        ball.Drop();
    }
}