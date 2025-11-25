using UnityEngine;

[DisallowMultipleComponent]
public class OpponentAIBrain : MonoBehaviour
{
    public PlayerController pc;

    [Header("PERSECUCIÓN")]
    public float chaseBallSpeed = 1f; // IA mueve usando SetAIMoveInput
    public float minDistanceToStop = 0.05f;

    [Header("Hold y presión")]
    public Vector2 holdBallRange = new Vector2(0.35f, 0.8f);
    public float pressureRadius = 1.15f;
    public float actionCooldown = 0.5f;

    [Header("Tiro")]
    public float shootDistance = 4.5f;
    [Range(-1f, 1f)] public float minShotAlignmentCos = 0.15f;
    public float shotPower = 1f;

    [Header("Pase")]
    public float passConeAngle = 28f;
    public float passMinForward = 0.0f;

    private float _holdUntil = -1f;
    private float _lastActionAt = -999f;
    private PlayerController _lastOwner = null;

    void Awake()
    {
        if (!pc) pc = GetComponent<PlayerController>();
    }

    void Update()
    {
        var ball = BallController.Instance;
        if (!ball || !pc) return;

        var owner = ball.Owner;

        // 1) SI NO TENGO LA PELOTA → PERSEGUIRLA (pickup automático del BallController)
        if (owner != pc)
        {
            ChaseBall(ball);
            _holdUntil = -1f;
            return;
        }

        // 2) SI SOY DUEÑO → DECIDIR QUÉ HACER
        HandleBallOwnerLogic(ball);
    }

    private void ChaseBall(BallController ball)
    {
        Vector2 ballPos = ball.transform.position;
        Vector2 myPos = pc.transform.position;

        Vector2 dir = (ballPos - myPos);

        // evitar vibraciones cuando ya está muy cerca
        if (dir.magnitude < minDistanceToStop)
        {
            pc.SetAIMoveInput(Vector2.zero);
            return;
        }

        dir.Normalize();
        pc.SetAIMoveInput(dir); 
    }

    private void HandleBallOwnerLogic(BallController ball)
    {
        var owner = ball.Owner;

        // Detectar recepción
        if (owner != _lastOwner)
        {
            _lastOwner = owner;
            if (owner == pc)
                _holdUntil = Time.time + Random.Range(holdBallRange.x, holdBallRange.y);
        }

        if (MatchTimer.CountdownActive) return;

        // cooldown
        if (Time.time - _lastActionAt < actionCooldown) return;

        bool underPressure = IsOpponentNear(pressureRadius);
        bool canDecide = (Time.time >= _holdUntil) || underPressure;
        if (!canDecide) return;

        // 1) ¿Tiro a portería?
        if (HasGoodShot(ball))
        {
            pc.AIShootAtGoal(0.10f, shotPower);
            _lastActionAt = Time.time;
            return;
        }

        // 2) ¿Busco pase?
        Vector2 preferredDir = GoalDirection();
        pc.AIPass(preferredDir, passConeAngle, passMinForward);
        _lastActionAt = Time.time;
    }

    private bool HasGoodShot(BallController ball)
    {
        if (!pc.leftPost || !pc.rightPost) return false;

        Vector2 mid = 0.5f * ((Vector2)pc.leftPost.position + (Vector2)pc.rightPost.position);
        Vector2 toGoal = mid - (Vector2)ball.transform.position;

        if (toGoal.magnitude > shootDistance) return false;

        Vector2 facing = (pc.transform.localScale.x >= 0f) ? Vector2.right : Vector2.left;
        float cos = Vector2.Dot(facing.normalized, toGoal.normalized);

        return cos >= minShotAlignmentCos;
    }

    private Vector2 GoalDirection()
    {
        if (pc.leftPost && pc.rightPost)
        {
            Vector2 mid = 0.5f * ((Vector2)pc.leftPost.position + (Vector2)pc.rightPost.position);
            return (mid - (Vector2)pc.transform.position).normalized;
        }

        return (pc.transform.localScale.x >= 0f) ? Vector2.right : Vector2.left;
    }

    private bool IsOpponentNear(float radius)
    {
        var all = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            if (p == null || p == pc) continue;
            if (!p.teamId.Equals(pc.teamId))
            {
                if (((Vector2)p.transform.position - (Vector2)pc.transform.position).sqrMagnitude <= radius * radius)
                    return true;
            }
        }
        return false;
    }
}
