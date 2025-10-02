using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class InputBridge : MonoBehaviour
{
    PlayerController pc;

    void Awake() => pc = GetComponent<PlayerController>();

    public void OnMove(InputAction.CallbackContext ctx)
        => pc.SetMoveInput(ctx.ReadValue<Vector2>());

    public void OnPass(InputAction.CallbackContext ctx)
    { if (ctx.performed) pc.ActionPass(); }

    public void OnShoot(InputAction.CallbackContext ctx)
    { if (ctx.performed) pc.ActionShoot(); }

    public void OnDrop(InputAction.CallbackContext ctx)
    { if (ctx.performed) pc.ActionDrop(); }
}