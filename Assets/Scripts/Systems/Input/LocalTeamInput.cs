using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class LocalTeamInput : MonoBehaviour
{
    [Header("Team you control")]
    public TeamId localTeam = TeamId.Blue;

    [Header("Optional camera follow")]
    public CameraFollow cameraFollow;

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

    void Start() => TryEnsureActivePlayer("Start");

    void Update()
    {
        if (!activePlayer) TryEnsureActivePlayer("Update fallback");
    }

    void TryEnsureActivePlayer(string src)
    {
        var ball = BallController.Instance;

        if (ball && ball.Owner && ball.Owner.teamId == localTeam)
        {
            SetActivePlayer(ball.Owner, src + " owner");
            return;
        }

        var near = TeamRegistry.GetClosestTeammateToBall(localTeam);
        if (near) { SetActivePlayer(near, src + " nearest"); return; }

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
        if (cameraFollow) cameraFollow.Target = activePlayer.transform;
        // Debug.Log($"[LocalTeamInput] Active={p.name} via {reason}");
    }

    // ---------------------------
    // PlayerInput (Send Messages)
    // ---------------------------

    // MOVE: Vector2
    public void OnMove(InputValue value)
    {
        if (!activePlayer) return;
        var v = value.Get<Vector2>();
        activePlayer.SetMoveInput(v);
        // Debug.Log($"[Input] Move {v}");
    }

    // PASS: mouse left (button -> float>0). Se llama en started/performed/canceled.
    public void OnPass(InputValue value)
    {
        if (!activePlayer) return;
        var pressed = value.isPressed || value.Get<float>() > 0f;
        if (pressed) activePlayer.ActionPass();
        // Debug.Log("[Input] Pass");
    }

    // SHOOT: mouse right
    public void OnShoot(InputValue value)
    {
        if (!activePlayer) return;
        var pressed = value.isPressed || value.Get<float>() > 0f;
        if (pressed) activePlayer.ActionShoot();
        // Debug.Log("[Input] Shoot");
    }

    // DROP: key F
    public void OnDrop(InputValue value)
    {
        if (!activePlayer) return;
        var pressed = value.isPressed || value.Get<float>() > 0f;
        if (pressed) activePlayer.ActionDrop();
        // Debug.Log("[Input] Drop");
    }
}
