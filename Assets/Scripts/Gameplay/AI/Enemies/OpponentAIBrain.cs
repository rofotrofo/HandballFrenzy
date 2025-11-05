using UnityEngine;

[DisallowMultipleComponent]
public class OpponentAIBrain : MonoBehaviour
{
    public PlayerController pc;

    [Header("Decisiones de pase")]
    [Tooltip("Tiempo mínimo y máximo que la IA retiene el balón antes de pasar.")]
    public Vector2 holdBallRange = new Vector2(0.45f, 0.9f);

    [Tooltip("Si hay un rival humano/enemigo muy cerca, intentamos pasar antes.")]
    public float pressureRadius = 1.2f;

    [Tooltip("Cooldown entre decisiones de pase (evita spam).")]
    public float passCooldown = 0.5f;

    private float _holdUntil = -1f;
    private float _lastPassAt = -999f;
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
                // Acabamos de recibir: esperar un ratito antes de pasar
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

        // ¿Bajo presión? (un jugador del otro equipo muy cerca)
        bool underPressure = IsOpponentNear(pressureRadius);

        // ¿Ya pasó el hold? ¿y respetamos cooldown?
        bool timeToPass = (Time.time >= _holdUntil) || underPressure;
        bool cooldownReady = (Time.time - _lastPassAt) >= passCooldown;

        if (timeToPass && cooldownReady)
        {
            pc.ActionPass();
            _lastPassAt = Time.time;
        }
    }

    private bool IsOpponentNear(float radius)
    {
        // ✅ Versión actualizada para Unity 6
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
