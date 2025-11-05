using UnityEngine;

[DisallowMultipleComponent]
public class OpponentAIBrain : MonoBehaviour
{
    public PlayerController pc;

    [Header("Hold y presión")]
    [Tooltip("Tiempo mínimo y máximo que la IA retiene el balón antes de decidir.")]
    public Vector2 holdBallRange = new Vector2(0.35f, 0.8f);
    [Tooltip("Si un oponente entra a este radio, aceleramos la decisión (pase/tiro).")]
    public float pressureRadius = 1.15f;
    [Tooltip("Cooldown entre acciones (evita spam).")]
    public float actionCooldown = 0.5f;

    [Header("Tiro")]
    [Tooltip("Distancia a portería dentro de la cual consideramos tirar.")]
    public float shootDistance = 4.5f;
    [Tooltip("Qué tan alineado al arco debe estar (cos del ángulo respecto al vector a portería). 1=perfecto, 0=90°")]
    [Range(-1f, 1f)] public float minShotAlignmentCos = 0.15f;
    [Tooltip("Fuerza base del tiro de IA.")]
    public float shotPower = 1f;

    [Header("Pase")]
    [Tooltip("Ángulo del cono para seleccionar compañero hacia portería.")]
    public float passConeAngle = 28f;
    [Tooltip("Componente mínima 'hacia adelante' para entrar al cono.")]
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

        // Detectar cambio de dueño
        if (owner != _lastOwner)
        {
            _lastOwner = owner;
            if (owner == pc)
            {
                // Acabamos de recibir: esperar un ratito antes de decidir
                _holdUntil = Time.time + Random.Range(holdBallRange.x, holdBallRange.y);
            }
            else
            {
                _holdUntil = -1f;
            }
        }

        // Si no soy dueño, no hago nada
        if (owner != pc) return;

        // Si estamos en cuenta regresiva (saque), no hacer nada
        if (MatchTimer.CountdownActive) return;

        bool cooldownReady = (Time.time - _lastActionAt) >= actionCooldown;
        if (!cooldownReady) return;

        // ¿Bajo presión?
        bool underPressure = IsOpponentNear(pressureRadius);

        // ¿Ya pasó el hold o estamos bajo presión?
        bool canDecide = (Time.time >= _holdUntil) || underPressure;
        if (!canDecide) return;

        // ====== TIRO O PASE ======
        if (HasGoodShot(ball))
        {
            // Tiro a portería (usa los postes configurados en el PlayerController)
            pc.AIShootAtGoal(spread: 0.10f, power: shotPower);
            _lastActionAt = Time.time;
            return;
        }

        // Pase preferente hacia la portería rival
        Vector2 preferredDir = GoalDirection(ball);
        pc.AIPass(preferredDir, passConeAngle, passMinForward);
        _lastActionAt = Time.time;
    }

    private bool HasGoodShot(BallController ball)
    {
        if (!pc.leftPost || !pc.rightPost) return false;

        // Punto medio de la portería
        Vector2 mid = 0.5f * ((Vector2)pc.leftPost.position + (Vector2)pc.rightPost.position);
        Vector2 fromBallToGoal = (mid - (Vector2)ball.transform.position);
        float dist = fromBallToGoal.magnitude;
        if (dist > shootDistance) return false;

        // Alineación: si nos estamos moviendo/hacia dónde apuntamos relativo al arco
        Vector2 facing = (pc.transform.localScale.x >= 0f) ? Vector2.right : Vector2.left; // flip lateral básico
        float cos = Vector2.Dot(facing.normalized, fromBallToGoal.normalized);
        return cos >= minShotAlignmentCos;
    }

    private Vector2 GoalDirection(BallController ball)
    {
        if (pc.leftPost && pc.rightPost)
        {
            Vector2 mid = 0.5f * ((Vector2)pc.leftPost.position + (Vector2)pc.rightPost.position);
            return (mid - (Vector2)pc.transform.position).normalized;
        }
        // fallback: adelante local
        return (pc.transform.localScale.x >= 0f) ? Vector2.right : Vector2.left;
    }

    private bool IsOpponentNear(float radius)
    {
        var all = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            if (p == null || p == pc) continue;
            // Oponente = distinto teamId
            if (!p.teamId.Equals(pc.teamId))
            {
                if (((Vector2)p.transform.position - (Vector2)pc.transform.position).sqrMagnitude <= radius * radius)
                    return true;
            }
        }
        return false;
    }
}
