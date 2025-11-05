using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class TeammateAI : MonoBehaviour
{
    // ====== LOCK LIGERO DE SPOTS (por equipo + banco) ======
    // Un "claim" por frame para cada indice de spot, evita que 2 IAs del mismo equipo elijan el mismo.
    static class SpotLock
    {
        // Clave: (teamId, bankType, spotIndex)
        struct Key
        {
            public TeamId team;
            public int bankType; // 0=Attack,1=Defend,2=Neutral
            public int index;
            public Key(TeamId t, int b, int i){ team=t; bankType=b; index=i; }
        }

        static Dictionary<Key,int> _owners = new Dictionary<Key,int>(128);

        // Debe llamarse una vez por frame antes de que los bots reclamen spots (lo hacemos implícito con frameId)
        static int _frameId = -1;
        static void BeginFrame()
        {
            int f = Time.frameCount;
            if (f != _frameId)
            {
                _owners.Clear();
                _frameId = f;
            }
        }

        public static bool TryClaim(TeamId team, int bankType, int index, int instanceId)
        {
            BeginFrame();
            var k = new Key(team, bankType, index);
            if (_owners.TryGetValue(k, out var owner))
            {
                return owner == instanceId; // ya lo tenía este mismo
            }
            _owners[k] = instanceId;
            return true;
        }
    }

    // ----------------------------------------

    public PlayerController pc;

    [Header("Slot en la formación (0=defensa,1=medio,2=ataque)")]
    [Range(0,2)] public int slotIndex = 1;

    [Header("Zonas locales (opcional si usas TeamZones)")]
    public Transform[] attackSpots;   // tamaño 3 opcional
    public Transform[] defendSpots;   // tamaño 3 opcional
    public Transform[] neutralSpots;  // tamaño 3 opcional

    [Header("Movimiento")]
    public float arriveRadius = 0.15f;
    public float slowRadius   = 1.2f;
    [Tooltip("Radio de órbita para micro-desencimado visual al llegar al spot.")]
    public float orbitRadius  = 0.18f;

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

    // Cache para micro-orbita determinista
    private float _orbitAngle;

    void Awake()
    {
        if (!pc) pc = GetComponent<PlayerController>();
        // Ángulo determinista por agente (no cambia en runtime)
        _orbitAngle = (Mathf.Abs(GetInstanceID() % 360)) * Mathf.Deg2Rad;
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
            s_noOwnerSince = -1f;
            s_prevOwnerTeam = owner.teamId;
        }
        else
        {
            if (s_noOwnerSince < 0f) s_noOwnerSince = Time.time;
        }

        bool haveOwner = owner != null;

        // ourBall “efectivo” con retardo
        bool ourBallEffective = false;
        if (haveOwner) ourBallEffective = (owner.teamId == pc.teamId);
        else
        {
            bool weWereLast = (s_prevOwnerTeam.HasValue && s_prevOwnerTeam.Value.Equals(pc.teamId));
            bool delayActive = (s_noOwnerSince >= 0f) && ((Time.time - s_noOwnerSince) < neutralDelayAfterNoOwner);
            ourBallEffective = weWereLast && delayActive;
        }

        // ====== Selección de banco según estado EFECTIVO ======
        Transform[] bank = GetBank(ourBallEffective, haveOwner, out int bankType);
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

        // 3) Elegir spot (con stickiness, closest-wins, tie-break por bias)
        int chosen = ChooseSpot(bank, occupied, reservedForPlayer);

        // 3.1) Intentar CLAIM de spot (lock por frame). Si está tomado, buscar otra opción libre.
        if (chosen >= 0)
        {
            if (!SpotLock.TryClaim(pc.teamId, bankType, chosen, GetInstanceID()))
            {
                // Busca otra alternativa libre que pueda reclamar
                chosen = FindFirstClaimable(bank, occupied, reservedForPlayer, bankType);
            }
        }

        if (chosen != _currentAssignedIndex)
        {
            _currentAssignedIndex = chosen;
            _lastAssignTime = Time.time;
        }

        Vector2 targetPos = (chosen >= 0) ? (Vector2)bank[chosen].position
                                          : DynamicFallback(ourBallEffective, haveOwner, slotIndex);

        // Aplicar micro-órbita (separa visualmente aunque tengan el mismo target lógico)
        targetPos += OrbitOffset();

        MoveTowardsWithSeparation(targetPos);
    }

    // Encuentra el primer spot que además pueda "reclamar"
    int FindFirstClaimable(Transform[] bank, HashSet<int> occupied, int reservedForPlayer, int bankType)
    {
        int n = bank.Length;
        // 1) intentar por slotIndex
        if (IsFree(bank, n, slotIndex, occupied, reservedForPlayer) &&
            SpotLock.TryClaim(pc.teamId, bankType, slotIndex, GetInstanceID()))
            return slotIndex;

        // 2) por cercanía
        int best = -1; float bestD2 = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            if (!IsFree(bank, n, i, occupied, reservedForPlayer)) continue;
            if (!SpotLock.TryClaim(pc.teamId, bankType, i, GetInstanceID())) continue;

            float d2 = (bank[i].position - transform.position).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = i; }
        }
        return best;
    }

    Vector2 OrbitOffset()
    {
        if (orbitRadius <= 0f) return Vector2.zero;
        // Pequeña rotación contínua para no quedarse estático uno sobre otro
        float a = _orbitAngle + (Time.time * 0.35f); // velocidad bajita
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * orbitRadius;
    }

    // -------------------- Asignación de spot --------------------

    // Sesgo tiny para desempatar cercanías en el MISMO FRAME
    float BiasFor(PlayerController who)
    {
        if (!who) return 0f;
        return Mathf.Abs(who.GetInstanceID() % 997) * 1e-6f; // tiny bias
    }

    float DistanceSqrWithBias(Transform spot, PlayerController who)
    {
        if (!spot || !who) return float.PositiveInfinity;
        float d2 = (spot.position - who.transform.position).sqrMagnitude;
        return d2 + BiasFor(who);
    }

    bool IsFree(Transform[] bank, int n, int idx, HashSet<int> occupied, int reservedForPlayer)
    {
        return idx >= 0 && idx < n && bank[idx] != null && !occupied.Contains(idx) && idx != reservedForPlayer;
    }

    int ChooseSpot(Transform[] bank, HashSet<int> occupied, int reservedForPlayer)
    {
        int n = bank.Length;

        bool Free(int idx) => IsFree(bank, n, idx, occupied, reservedForPlayer);

        bool IAmClosest(int idx)
        {
            if (idx < 0 || idx >= n || bank[idx] == null) return false;

            float myD = DistanceSqrWithBias(bank[idx], pc);

            var human = PlayerController.CurrentControlled;
            if (human && human.teamId == pc.teamId)
            {
                float hd = DistanceSqrWithBias(bank[idx], human);
                if (hd <= myD - (stickinessGain * stickinessGain)) return false;
            }

            var others = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var other in others)
            {
                if (other == null || other.teamId != pc.teamId || other == pc) continue;

                var ai = other.GetComponent<TeammateAI>();
                if (!ai) continue;
                if (other == PlayerController.CurrentControlled) continue;

                bool aiOurBall, aiHaveOwner;
                GetStateFor(ai, out aiOurBall, out aiHaveOwner);
                var aiBank = ai.GetBank(aiOurBall, aiHaveOwner, out _);
                if (aiBank != bank) continue;

                float od = DistanceSqrWithBias(bank[idx], other);
                if (od <= myD - (stickinessGain * stickinessGain)) return false;
            }
            return true;
        }

        bool CooldownActive() => (Time.time - _lastAssignTime) < reassignmentCooldown;

        // Mantener spot actual si sigue siendo válido
        if (_currentAssignedIndex >= 0 && _currentAssignedIndex < n && bank[_currentAssignedIndex] != null)
        {
            bool reservedHit = (_currentAssignedIndex == reservedForPlayer);
            bool stillFree = Free(_currentAssignedIndex);
            bool closest = IAmClosest(_currentAssignedIndex);

            if (!reservedHit && stillFree && (closest || CooldownActive()))
                return _currentAssignedIndex;
        }

        // Intentar el spot por defecto de mi slot, si es libre y soy el más cercano
        if (Free(slotIndex) && IAmClosest(slotIndex))
            return slotIndex;

        // Buscar el mejor libre donde YO sea el más cercano (con bias)
        int best = -1; float bestD2 = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            if (!Free(i)) continue;
            if (!IAmClosest(i)) continue;

            float d2 = DistanceSqrWithBias(bank[i], pc);
            if (d2 < bestD2) { bestD2 = d2; best = i; }
        }
        if (best >= 0) return best;

        // Si no hay ninguno donde yo sea el más cercano, elegir el más cercano libre
        best = -1; bestD2 = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            if (!Free(i)) continue;
            float d2 = DistanceSqrWithBias(bank[i], pc);
            if (d2 < bestD2) { bestD2 = d2; best = i; }
        }
        return best;
    }

    // -------------------- Helpers de ocupación/estado --------------------

    Transform[] GetBank(bool ourBall, bool haveOwner, out int bankType)
    {
        // bankType: 0 attack, 1 defend, 2 neutral
        Transform[] localBank = null;

        if (ourBall) { localBank = attackSpots; bankType = 0; }
        else if (haveOwner) { localBank = defendSpots; bankType = 1; }
        else { localBank = neutralSpots; bankType = 2; }

        bool completeLocal = localBank != null && localBank.Length >= 3 && localBank.All(t => t != null);
        if (completeLocal) return localBank;

        var tz = TeamZones.Instance;
        if (!tz) { bankType = 2; return null; }

        if (ourBall) { bankType = 0; return tz.attackSpots; }
        else if (haveOwner) { bankType = 1; return tz.defendSpots; }
        else { bankType = 2; return tz.neutralSpots; }
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
        var allPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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
            var aiBank = ai.GetBank(aiOurBall, aiHaveOwner, out _);
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
        var all = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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
