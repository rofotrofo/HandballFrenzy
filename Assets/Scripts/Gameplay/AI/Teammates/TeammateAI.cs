using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class TeammateAI : MonoBehaviour
{
    public PlayerController pc;

    [Header("Slot en la formación (0=defensa, 1=medio, 2=ataque)")]
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

    // índice actual que este bot cree tener asignado en el banco actual (-1 si ninguno)
    private int _currentAssignedIndex = -1;

    // Espera 1 frame para que CurrentControlled se establezca
    private bool _waitedOneFrame = false;

    void Awake()
    {
        if (!pc) pc = GetComponent<PlayerController>();
    }

    void LateUpdate()
    {
        if (!_waitedOneFrame) { _waitedOneFrame = true; return; }

        if (MatchTimer.CountdownActive) { pc.SetAIMoveInput(Vector2.zero); return; }

        // Si este jugador es el controlado por humano, no usamos IA
        if (pc == PlayerController.CurrentControlled) { pc.SetAIMoveInput(Vector2.zero); _currentAssignedIndex = -1; return; }

        var ball = BallController.Instance;

        bool haveOwner = ball && ball.Owner != null;
        bool ourBall = haveOwner && (ball.Owner.teamId == pc.teamId);

        // Seleccionar banco de spots según estado
        Transform[] bank = GetBank(ourBall, haveOwner);
        if (bank == null || bank.Length == 0)
        {
            // Fallback sin zonas: ir a un objetivo dinámico
            Vector2 dyn = DynamicFallback(ourBall, haveOwner, slotIndex);
            MoveTowards(dyn);
            _currentAssignedIndex = -1;
            return;
        }

        // Determinar si el jugador humano reserva un spot
        int reservedForPlayer = -1;
        var human = PlayerController.CurrentControlled;
        if (human != null && human.teamId == pc.teamId)
        {
            reservedForPlayer = ClosestSpotIndex(bank, human.transform.position, playerClaimRadius);
        }

        // Conjunto de ocupados por otros bots de mi equipo (únicamente IA activas)
        var occupied = GetOccupiedSpotIndicesByMyTeam(bank, exclude: this);

        // Si el humano reserva uno, añádelo como ocupado
        if (reservedForPlayer >= 0) occupied.Add(reservedForPlayer);

        // Si yo estaba ocupando uno que ahora queda reservado por humano, suéltalo
        if (_currentAssignedIndex == reservedForPlayer) _currentAssignedIndex = -1;

        // Elegir asignación: preferir slotIndex, luego el libre más cercano
        int desired = slotIndex;
        int assigned = -1;

        if (!IsIndexFree(desired, occupied, bank.Length) || desired == reservedForPlayer)
        {
            assigned = PickNearestFreeIndex(bank, occupied, transform.position);
        }
        else
        {
            assigned = desired;
        }

        // Marca como ocupado (localmente); si otro bot eligió igual en este frame,
        // el siguiente frame se resuelve (con 2 bots funciona estable).
        _currentAssignedIndex = assigned;

        // Mover hacia el spot asignado, o fallback si no hay
        Vector2 targetPos = (assigned >= 0) ? (Vector2)bank[assigned].position
                                            : DynamicFallback(ourBall, haveOwner, slotIndex);

        MoveTowards(targetPos);
    }

    // -------------------- Helpers de asignación --------------------

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
        float bestDist = maxRadius * maxRadius;
        for (int i = 0; i < bank.Length; i++)
        {
            if (bank[i] == null) continue;
            float d2 = (bank[i].position - pos).sqrMagnitude;
            if (d2 <= bestDist)
            {
                bestDist = d2;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    HashSet<int> GetOccupiedSpotIndicesByMyTeam(Transform[] bank, TeammateAI exclude)
    {
        var set = new HashSet<int>();
        // Intenta usar TeamRegistry si lo tienes; de lo contrario, busca en escena
        var allPlayers = Object.FindObjectsOfType<PlayerController>();
        foreach (var p in allPlayers)
        {
            if (p == null || p.teamId != pc.teamId) continue;
            if (p == PlayerController.CurrentControlled) continue; // humano no como bot

            var ai = p.GetComponent<TeammateAI>();
            if (!ai || ai == exclude) continue;

            // Solo considerar si ese AI está usando este mismo banco (mismo estado)
            bool aiOurBall, aiHaveOwner;
            GetStateFor(ai, out aiOurBall, out aiHaveOwner);
            var aiBank = ai.GetBank(aiOurBall, aiHaveOwner);
            if (aiBank != bank) continue; // bancos distintos (otro estado), no chocan

            int idx = ai._currentAssignedIndex;
            if (idx >= 0 && idx < bank.Length) set.Add(idx);
            else
            {
                // Si no tiene asignación aún, infiere por cercanía al spot más próximo (suaviza primeros frames)
                int guess = ClosestSpotIndex(bank, ai.transform.position, Mathf.Infinity);
                if (guess >= 0) set.Add(guess);
            }
        }
        return set;
    }

    void GetStateFor(TeammateAI other, out bool ourBall, out bool haveOwner)
    {
        var ball = BallController.Instance;
        haveOwner = ball && ball.Owner != null;
        ourBall = haveOwner && (ball.Owner.teamId == other.pc.teamId);
    }

    bool IsIndexFree(int idx, HashSet<int> occupied, int bankLen)
    {
        return idx >= 0 && idx < bankLen && !occupied.Contains(idx);
    }

    int PickNearestFreeIndex(Transform[] bank, HashSet<int> occupied, Vector3 fromPos)
    {
        int bestIdx = -1;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < bank.Length; i++)
        {
            if (bank[i] == null) continue;
            if (occupied.Contains(i)) continue;

            float d2 = (bank[i].position - fromPos).sqrMagnitude;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    // -------------------- Movimiento --------------------

    void MoveTowards(Vector2 targetPos)
    {
        Vector2 to = targetPos - (Vector2)transform.position;
        float dist = to.magnitude;

        if (dist <= arriveRadius) { pc.SetAIMoveInput(Vector2.zero); return; }

        Vector2 desiredDir = to / Mathf.Max(dist, 0.0001f);

        float factor = (dist < slowRadius) ? Mathf.InverseLerp(arriveRadius, slowRadius, dist) : 1f;
        desiredDir *= Mathf.Clamp01(factor);

        pc.SetAIMoveInput(desiredDir);
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
