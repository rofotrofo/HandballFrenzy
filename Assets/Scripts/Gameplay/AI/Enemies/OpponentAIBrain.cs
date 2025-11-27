using UnityEngine;

[DisallowMultipleComponent]
public class OpponentAIBrain : MonoBehaviour
{
    public PlayerController pc;

    [Header("PERSECUCIÓN")]
    public float chaseBallSpeed = 1f;
    public float minDistanceToStop = 0.05f;

    [Header("Tiempo de reacción cuando la pelota está suelta/lejana")]
    public float looseBallMinDelay = 0.20f;
    public float looseBallMaxDelay = 0.45f;
    private float _looseBallWaitUntil = -1f;

    [Header("Inteligencia de formación (Ball Chaser)")]
    public bool isBallChaser = false;
    public float reassignChaserEvery = 1.25f;
    private static float _lastChaserAssign = -999f;

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

        AssignBallChaser(ball);

        var owner = ball.Owner;

        // 1) NO tengo la pelota
        if (owner != pc)
        {
            HandleLooseBallState(ball);
            return;
        }

        // 2) SÍ tengo la pelota
        HandleBallOwnerLogic(ball);
    }

    // ================================================================
    //               ASIGNAR CHASER REALMENTE ENTRE IA
    // ================================================================
    private void AssignBallChaser(BallController ball)
    {
        if (Time.time - _lastChaserAssign < reassignChaserEvery)
            return;

        _lastChaserAssign = Time.time;

        OpponentAIBrain closestAI = null;
        float best = 999f;

        // Buscar SOLO IA del mismo equipo
        foreach (var ai in Object.FindObjectsByType<OpponentAIBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!ai || !ai.pc) continue;
            if (!ai.pc.teamId.Equals(pc.teamId)) continue;

            float d = Vector2.Distance(ai.pc.transform.position, ball.transform.position);
            if (d < best)
            {
                best = d;
                closestAI = ai;
            }
        }

        // Asignar el chaser único
        if (closestAI)
        {
            foreach (var ai in Object.FindObjectsByType<OpponentAIBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                ai.isBallChaser = false;

            closestAI.isBallChaser = true;
        }
    }

    // ================================================================
    //              MANEJO DE PELOTA SUELTA O LEJANA
    // ================================================================
    private void HandleLooseBallState(BallController ball)
    {
        float dist = Vector2.Distance(pc.transform.position, ball.transform.position);

        // NUEVA LÓGICA: pelota suelta tácticamente cuando:
        bool tacticallyLoose = (ball.Owner == null) || (dist > 3f);

        if (tacticallyLoose)
        {
            if (_looseBallWaitUntil < 0)
                _looseBallWaitUntil = Time.time + Random.Range(looseBallMinDelay, looseBallMaxDelay);

            if (Time.time < _looseBallWaitUntil)
            {
                pc.SetAIMoveInput(Vector2.zero);
                return;
            }
        }
        else
        {
            _looseBallWaitUntil = -1;
        }

        // Si SOY el chaser → ir directo
        if (isBallChaser)
        {
            ChaseBall(ball);
            return;
        }

        // SI NO SOY el chaser → persecución solo si estoy muy cerca
        if (dist < 1.2f)
        {
            ChaseBallWithSeparation(ball);
        }
        else
        {
            pc.SetAIMoveInput(Vector2.zero); // zona neutral
        }
    }

    // ================================================================
    //                 PERSECUCIÓN PRINCIPAL DEL CHASER
    // ================================================================
    private void ChaseBall(BallController ball)
    {
        Vector2 dir = (ball.transform.position - pc.transform.position);

        if (dir.magnitude < minDistanceToStop)
        {
            pc.SetAIMoveInput(Vector2.zero);
            return;
        }

        pc.SetAIMoveInput(dir.normalized);
    }

    // ================================================================
    //         PERSECUCIÓN PARA LOS QUE NO SON CHASER (con separación)
    // ================================================================
    private void ChaseBallWithSeparation(BallController ball)
    {
        Vector2 chaseDir = (ball.transform.position - pc.transform.position).normalized;
        Vector2 separation = Vector2.zero;

        foreach (var mate in Object.FindObjectsByType<OpponentAIBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (mate == this) continue;
            if (!mate.pc) continue;
            if (!mate.pc.teamId.Equals(pc.teamId)) continue;

            float d2 = Vector2.Distance(mate.pc.transform.position, pc.transform.position);
            if (d2 < 0.75f)
            {
                separation += (Vector2)(pc.transform.position - mate.pc.transform.position).normalized * 0.55f;
            }
        }

        Vector2 finalDir = (chaseDir + separation).normalized;
        pc.SetAIMoveInput(finalDir);
    }

    // ================================================================
    //                   LÓGICA DE DUEÑO DE BALÓN
    // ================================================================
    private void HandleBallOwnerLogic(BallController ball)
    {
        var owner = ball.Owner;

        if (owner != _lastOwner)
        {
            _lastOwner = owner;
            if (owner == pc)
                _holdUntil = Time.time + Random.Range(holdBallRange.x, holdBallRange.y);
        }

        if (MatchTimer.CountdownActive) return;
        if (Time.time - _lastActionAt < actionCooldown) return;

        bool underPressure = IsOpponentNear(pressureRadius);
        bool canDecide = (Time.time >= _holdUntil) || underPressure;
        if (!canDecide) return;

        if (HasGoodShot(ball))
        {
            pc.AIShootAtGoal(0.10f, shotPower);
            _lastActionAt = Time.time;
            return;
        }

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
