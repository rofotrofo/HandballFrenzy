using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class TeammateAI : MonoBehaviour
{
    public PlayerController pc;

    [Header("Slot en la formación (0=defensa,1=medio,2=ataque)")]
    [Range(0,2)] public int slotIndex = 1;

    [Header("Zonas locales (opcional si usas TeamZones)")]
    public Transform[] attackSpots;   // tamaño 3 opcional
    public Transform[] defendSpots;   // tamaño 3 opcional
    public Transform[] neutralSpots;  // tamaño 3 opcional

    [Header("Movimiento")]
    public float arriveRadius = 0.15f;
    public float slowRadius = 1.2f;

    [Header("Coordinación de spots")]
    [Tooltip("Si el jugador humano entra en este radio de un spot, ese spot queda reservado para él.")]
    public float playerClaimRadius = 0.8f;
    [Tooltip("Radio para considerar que un spot está 'ocupado' por cercanía.")]
    public float occupancyRadius = 0.45f;
    [Tooltip("Ventaja mínima de distancia (m) para permitir que otro te quite el spot.")]
    public float stickinessGain = 0.15f;
    [Tooltip("Tiempo mínimo antes de permitir re-asignación de spot (s).")]
    public float reassignmentCooldown = 0.35f;

    [Header("Separación entre compañeros")]
    public float separationRadius = 0.9f;
    public float separationWeight = 0.6f; // 0 = sin separación

    [Header("Retardo para ir a neutro")]
    [Tooltip("Tiempo que permanecemos en modo ATAQUE tras quedar sin dueño el balón, antes de pasar a NEUTRO.")]
    public float neutralDelayAfterNoOwner = 2f;

    // ===== Memoria compartida de posesión (para los 3 jugadores) =====
    private static TeamId? s_prevOwnerTeam = null;  // último equipo que tuvo la pelota (null si nunca)
    private static float   s_noOwnerSince  = -1f;   // Time.time cuando quedó sin dueño (-1 si hay dueño)

    // Estado interno por bot
    private int _currentAssignedIndex = -1;
    private float _lastAssignTime = -999f;
    private bool _waitedOneFrame = false;

    void Awake()
    {
        if (!pc) pc = GetComponent<PlayerController>();
    }

    void LateUpdate()
    {
        if (!_waitedOneFrame) { _waitedOneFrame = true; return; }
        if (MatchTimer.CountdownActive) { pc.SetAIMoveInput(Vector2.zero); return; }

        // Humano -> IA off
        if (pc == PlayerController.CurrentControlled) { pc.SetAIMoveInput(Vector2.zero); _currentAssignedIndex = -1; return; }

        // ====== Lectura y memoria de posesión ======
        var ball  = BallController.Instance;
        var owner = (ball != null) ? ball.Owner : null;

        if (owner != null)
        {
            // Hay dueño: resetea "sin dueño" y guarda equipo
            s_noOwnerSince = -1f;
            s_prevOwnerTeam = owner.teamId;
        }
        else
        {
            // Sin dueño: inicia o continúa el temporizador
            if (s_noOwnerSince < 0f) s_noOwnerSince = Time.time;
        }

        bool haveOwner = owner != null;

        // ourBall “efectivo” con retardo: si NO hay dueño pero aún no pasa el delay,
        // nos quedamos en ataque si el último dueño fue nuestro equipo.
        bool ourBallEffective = false;
        if (haveOwner)
        {
            ourBallEffective = (owner.teamId == pc.teamId);
        }
        else
        {
            bool weWereLast = (s_prevOwnerTeam.HasValue && s_prevOwnerTeam.Value.Equals(pc.teamId));
            bool delayActive = (s_noOwnerSince >= 0f) && ((Time.time - s_noOwnerSince) < neutralDelayAfterNoOwner);
            ourBallEffective = weWereLast && delayActive; // mantenemos ATAQUE durante el grace period
        }

        // ====== Selección de banco según estado EFECTIVO ======
        Transform[] bank = GetBank(ourBallEffective, haveOwner);
        if (bank == null || bank.Length == 0)
        {
            Vector2 dyn = DynamicFallback(ourBallEffective, haveOwner, slotIndex);
            MoveTowardsWithSeparation(dyn);
            _currentAssignedIndex = -1;
            return;
        }

        // 1) Reserva del humano
        int reservedForPlayer = -1;
        var human = PlayerController.CurrentControlled;
        if (human != null && human.teamId == pc.teamId)
            reservedForPlayer = ClosestSpotIndex(bank, human.transform.position, playerClaimRadius);

        // 2) Ocupados por mi equipo (IA activas) + humano
        var occupied = GetOccupiedSpotIndicesByMyTeam(bank, exclude: this);
        if (reservedForPlayer >= 0) occupied.Add(reservedForPlayer);

        // 3) Elegir spot (con stickiness, closest-wins, etc.)
        int chosen = ChooseSpot(bank, occupied, reservedForPlayer);

        if (chosen != _currentAssignedIndex)
        {
            _currentAssignedIndex = chosen;
            _lastAssignTime = Time.time;
        }

        Vector2 targetPos = (chosen >= 0) ? (Vector2)bank[chosen].position
                                          : DynamicFallback(ourBallEffective, haveOwner, slotIndex);

        MoveTowardsWithSeparation(targetPos);
    }

    // -------------------- Asignación de spot --------------------

    int ChooseSpot(Transform[] bank, HashSet<int> occupied, int reservedForPlayer)
    {
        int n = bank.Length;

        bool Free(int idx) => idx >= 0 && idx < n && !occupied.Contains(idx) && bank[idx] != null;

        bool IAmClosest(int idx)
        {
            if (idx < 0 || idx >= n || bank[idx] == null) return false;

            float myD = (bank[idx].position - transform.position).sqrMagnitude;

            var human = PlayerController.CurrentControlled;
            if (human && human.teamId == pc.teamId)
            {
                float hd = (bank[idx].position - human.transform.position).sqrMagnitude;
                if (hd <= myD - (stickinessGain * stickinessGain)) return false;
            }

            foreach (var other in FindObjectsOfType<PlayerController>())
            {
                if (other == null || other.teamId != pc.teamId || other == pc) continue;

                var ai = other.GetComponent<TeammateAI>();
                if (!ai) continue;
                if (other == PlayerController.CurrentControlled) continue;

                bool aiOurBall, aiHaveOwner;
                GetStateFor(ai, out aiOurBall, out aiHaveOwner);
                var aiBank = ai.GetBank(aiOurBall, aiHaveOwner);
                if (aiBank != bank) continue;

                float od = (bank[idx].position - other.transform.position).sqrMagnitude;
                if (od <= myD - (stickinessGain * stickinessGain)) return false;
            }
            return true;
        }

        bool CooldownActive() => (Time.time - _lastAssignTime) < reassignmentCooldown;

        if (_currentAssignedIndex >= 0 && _currentAssignedIndex < n && bank[_currentAssignedIndex] != null)
        {
            bool reservedHit = (_currentAssignedIndex == reservedForPlayer);
            bool stillFree = Free(_currentAssignedIndex);
            bool closest = IAmClosest(_currentAssignedIndex);

            if (!reservedHit && stillFree && (closest || CooldownActive()))
                return _currentAssignedIndex;
        }

        if (Free(slotIndex) && slotIndex != reservedForPlayer && IAmClosest(slotIndex))
            return slotIndex;

        int best = -1; float bestD2 = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            if (!Free(i) || i == reservedForPlayer) continue;
            if (!IAmClosest(i)) continue;

            float d2 = (bank[i].position - transform.position).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = i; }
        }
        if (best >= 0) return best;

        best = -1; bestD2 = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            if (!Free(i) || i == reservedForPlayer) continue;
            float d2 = (bank[i].position - transform.position).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = i; }
        }
        return best;
    }

    // -------------------- Helpers de ocupación/estado --------------------

    Transform[] GetBank(bool ourBall, bool haveOwner)
    {
        Transform[] localBank = null;
        if (ourBall) localBank = attackSpots;
        else if (haveOwner) localBank = defendSpots;
        else localBank = neutralSpots;

        bool completeLocal = localBank != null && localBank.Length >= 3 && localBank.All(t => t != null);
        if (completeLocal) return localBank;

        var tz = TeamZones.Instance;
        if (!tz) return null;

        if (ourBall) return tz.attackSpots;
        else if (haveOwner) return tz.defendSpots;
        else return tz.neutralSpots;
    }

    int ClosestSpotIndex(Transform[] bank, Vector3 pos, float maxRadius)
    {
        int bestIdx = -1;
        float bestD2 = maxRadius * maxRadius;
        for (int i = 0; i < bank.Length; i++)
        {
            if (bank[i] == null) continue;
            float d2 = (bank[i].position - pos).sqrMagnitude;
            if (d2 <= bestD2) { bestD2 = d2; bestIdx = i; }
        }
        return bestIdx;
    }

    HashSet<int> GetOccupiedSpotIndicesByMyTeam(Transform[] bank, TeammateAI exclude)
    {
        var set = new HashSet<int>();
        var allPlayers = Object.FindObjectsOfType<PlayerController>();
        foreach (var p in allPlayers)
        {
            if (p == null || p.teamId != pc.teamId) continue;

            if (p == PlayerController.CurrentControlled)
            {
                int pi = ClosestSpotIndex(bank, p.transform.position, occupancyRadius);
                if (pi >= 0) set.Add(pi);
                continue;
            }

            var ai = p.GetComponent<TeammateAI>();
            if (!ai || ai == exclude) continue;

            bool aiOurBall, aiHaveOwner;
            GetStateFor(ai, out aiOurBall, out aiHaveOwner);
            var aiBank = ai.GetBank(aiOurBall, aiHaveOwner);
            if (aiBank != bank) continue;

            int idx = ClosestSpotIndex(bank, ai.transform.position, occupancyRadius);
            if (idx >= 0) set.Add(idx);

            if (ai._currentAssignedIndex >= 0 && ai._currentAssignedIndex < bank.Length)
                set.Add(ai._currentAssignedIndex);
        }
        return set;
    }

    void GetStateFor(TeammateAI other, out bool ourBall, out bool haveOwner)
    {
        var ball = BallController.Instance;
        haveOwner = ball && ball.Owner != null;
        if (haveOwner) ourBall = (ball.Owner.teamId == other.pc.teamId);
        else
        {
            // Misma lógica de grace period para los otros (consistente)
            bool weWereLast = s_prevOwnerTeam.HasValue && s_prevOwnerTeam.Value.Equals(other.pc.teamId);
            bool delayActive = (s_noOwnerSince >= 0f) && ((Time.time - s_noOwnerSince) < other.neutralDelayAfterNoOwner);
            ourBall = weWereLast && delayActive;
        }
    }

    // -------------------- Movimiento + separación --------------------

    void MoveTowardsWithSeparation(Vector2 targetPos)
    {
        Vector2 to = targetPos - (Vector2)transform.position;
        float dist = to.magnitude;

        Vector2 dir = Vector2.zero;
        if (dist > arriveRadius)
        {
            dir = to / Mathf.Max(dist, 0.0001f);
            float factor = (dist < slowRadius) ? Mathf.InverseLerp(arriveRadius, slowRadius, dist) : 1f;
            dir *= Mathf.Clamp01(factor);
        }

        if (separationWeight > 0f && separationRadius > 0f)
        {
            Vector2 sep = ComputeSeparation();
            if (sep.sqrMagnitude > 0.000001f)
            {
                Vector2 combined = dir + sep * separationWeight;
                if (combined.sqrMagnitude > 0.000001f) dir = combined.normalized;
            }
        }

        pc.SetAIMoveInput(dir);
    }

    Vector2 ComputeSeparation()
    {
        Vector2 acc = Vector2.zero;
        var all = Object.FindObjectsOfType<PlayerController>();
        foreach (var other in all)
        {
            if (other == null || other.teamId != pc.teamId || other == pc) continue;

            Vector2 delta = (Vector2)transform.position - (Vector2)other.transform.position;
            float d = delta.magnitude;
            if (d <= 0.0001f || d > separationRadius) continue;

            float t = 1f - (d / separationRadius);
            acc += (delta / d) * t;
        }
        return acc;
    }

    // -------------------- Fallback dinámico --------------------

    Vector2 DynamicFallback(bool ourBall, bool haveOwner, int idx)
    {
        Vector2 me = transform.position;
        var ball = BallController.Instance;
        Vector2 ballPos = ball ? (Vector2)ball.transform.position : me;

        Vector2[] slotOffset = new Vector2[]
        {
            new Vector2(-1.5f, -0.6f),
            new Vector2( 0.0f,  0.0f),
            new Vector2( 1.5f,  0.6f)
        };
        Vector2 off = slotOffset[Mathf.Clamp(idx,0,2)];

        if (!haveOwner)     return Vector2.Lerp(ballPos, Vector2.zero, 0.3f) + off;
        if (ourBall)        return ballPos + (Vector2.right * 1.8f) + off;
        else
        {
            Vector2 ownGoalApprox = ballPos + Vector2.left * 10f;
            Vector2 mid = Vector2.Lerp(ballPos, ownGoalApprox, 0.35f);
            return mid + off * 0.6f;
        }
    }
}
