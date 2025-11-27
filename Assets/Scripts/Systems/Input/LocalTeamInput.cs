using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class LocalTeamInput : MonoBehaviour
{
    [Header("Team you control")]
    public TeamId localTeam = TeamId.Blue;

    [Header("Optional camera follow (NO se usa con mitades)")]
    public CameraFollow cameraFollow;

    // Solo lo usamos como respaldo inicial/visual; el input ya no depende de esto.
    private PlayerController activePlayer;

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
        // Ya NO sincronizamos cámara
    }

    void Update()
    {
        if (!activePlayer)
            TryEnsureActivePlayer("Update fallback");

        // Ya no existe sincronización de cámara con jugadores
        // porque la cámara funciona por mitades.
    }

    // ---------------------------
    // Descubrimiento inicial
    // ---------------------------

    void TryEnsureActivePlayer(string src)
    {
        var ball = BallController.Instance;

        if (ball && ball.Owner && ball.Owner.teamId == localTeam)
        {
            SetActivePlayer(ball.Owner, src + " owner");
            return;
        }

        var near = TeamRegistry.GetClosestTeammateToBall(localTeam);
        if (near)
        {
            SetActivePlayer(near, src + " nearest");
            return;
        }

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
        if (newOwner && newOwner.teamId == localTeam)
            SetActivePlayer(newOwner, "OnOwnerChanged");
    }

    void SetActivePlayer(PlayerController p, string reason)
    {
        if (!p) return;
        activePlayer = p;
    }

    // ---------------------------
    // PlayerInput
    // ---------------------------

    public void OnMove(InputValue value)
    {
        PlayerController.SetGlobalMoveInput(value.Get<Vector2>());
    }

    public void OnPass(InputValue value)
    {
        if (value.isPressed || value.Get<float>() > 0f)
            PlayerController.GlobalActionPass();
    }

    public void OnShoot(InputValue value)
    {
        if (value.isPressed || value.Get<float>() > 0f)
            PlayerController.GlobalActionShoot();
    }

    public void OnDrop(InputValue value)
    {
        if (value.isPressed || value.Get<float>() > 0f)
            PlayerController.GlobalActionDrop();
    }
}
