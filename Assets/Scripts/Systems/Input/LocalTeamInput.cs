using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class LocalTeamInput : MonoBehaviour
{
    [Header("Team you control")]
    public TeamId localTeam = TeamId.Blue;

    [Header("Optional camera follow")]
    public CameraFollow cameraFollow;

    // Solo lo usamos como respaldo inicial/visual; el input ya no depende de esto.
    private PlayerController activePlayer;

    // Cache para no estar reasignando la cámara cada frame si no cambió
    private Transform lastCameraTarget;

    void OnEnable()
    {
        var ball = BallController.Instance;
        if (ball) ball.OnOwnerChanged += HandleOwnerChanged;
    }

    void OnDisable()
    {
        var ball = BallController.Instance;
        if (ball) ball.OnOwnerChanged -= HandleOwnerChanged;
    }

    void Start()
    {
        TryEnsureActivePlayer("Start");
        SyncCameraToCurrentControlled();
    }

    void Update()
    {
        // Si por alguna razón no tenemos uno elegido aún (inicio de partido, sin dueño, etc.)
        if (!activePlayer) TryEnsureActivePlayer("Update fallback");

        // Mantén la cámara siguiendo al jugador actualmente controlado globalmente
        SyncCameraToCurrentControlled();
    }

    // ---------------------------
    // Descubrimiento de jugador inicial (solo para fallback/visual)
    // ---------------------------

    void TryEnsureActivePlayer(string src)
    {
        var ball = BallController.Instance;

        // 1) Si el balón tiene dueño de MI equipo
        if (ball && ball.Owner && ball.Owner.teamId == localTeam)
        {
            SetActivePlayer(ball.Owner, src + " owner");
            return;
        }

        // 2) Si no, tomar el más cercano a la pelota de mi equipo
        var near = TeamRegistry.GetClosestTeammateToBall(localTeam);
        if (near)
        {
            SetActivePlayer(near, src + " nearest");
            return;
        }

        // 3) Como último recurso, cualquiera de mi equipo
        var any = FindAnyLocalTeamPlayer();
        if (any) SetActivePlayer(any, src + " any");
        else Debug.LogWarning("[LocalTeamInput] No player found for local team");
    }

    PlayerController FindAnyLocalTeamPlayer()
    {
        var all = GameObject.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in all) if (p.teamId == localTeam) return p;
        return null;
    }

    void HandleOwnerChanged(PlayerController newOwner)
    {
        // Esto solo actualiza referencia local para fallback/visual.
        // El controlador REAL cambia en PlayerController internamente.
        if (newOwner && newOwner.teamId == localTeam)
            SetActivePlayer(newOwner, "OnOwnerChanged");
    }

    void SetActivePlayer(PlayerController p, string reason)
    {
        if (!p) return;
        activePlayer = p;
        // La cámara la sincronizamos con el CurrentControlled real, no con 'activePlayer'.
        SyncCameraToCurrentControlled();
        // Debug.Log($"[LocalTeamInput] Active (local) = {p.name} via {reason}");
    }

    void SyncCameraToCurrentControlled()
    {
        if (!cameraFollow) return;

        var cc = PlayerController.CurrentControlled;
        if (cc && cc.transform != lastCameraTarget)
        {
            cameraFollow.Target = cc.transform;
            lastCameraTarget = cc.transform;
        }
    }

    // ---------------------------
    // PlayerInput (Send Messages)
    // ---------------------------
    // AHORA todo el input se rutea a PlayerController.*Global* para que
    // siempre llegue al jugador controlado actual (CurrentControlled).

    // MOVE: Vector2
    public void OnMove(InputValue value)
    {
        var v = value.Get<Vector2>();
        PlayerController.SetGlobalMoveInput(v);
        // Debug.Log($"[Input] Move {v}");
    }

    // PASS: mouse left (button -> float>0). Se llama en started/performed/canceled.
    public void OnPass(InputValue value)
    {
        var pressed = value.isPressed || value.Get<float>() > 0f;
        if (pressed) PlayerController.GlobalActionPass();
        // Debug.Log("[Input] Pass");
    }

    // SHOOT: mouse right
    public void OnShoot(InputValue value)
    {
        var pressed = value.isPressed || value.Get<float>() > 0f;
        if (pressed) PlayerController.GlobalActionShoot();
        // Debug.Log("[Input] Shoot");
    }

    // DROP: key F
    public void OnDrop(InputValue value)
    {
        var pressed = value.isPressed || value.Get<float>() > 0f;
        if (pressed) PlayerController.GlobalActionDrop();
        // Debug.Log("[Input] Drop");
    }
}
