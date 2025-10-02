using UnityEngine;
using UnityEngine.InputSystem;

public class InputPulseDebug : MonoBehaviour
{
    public void OnMove(InputValue v)  { Debug.Log($"[Debug] Move {v.Get<Vector2>()}"); }
    public void OnPass(InputValue v)  { if (v.isPressed) Debug.Log("[Debug] Pass"); }
    public void OnShoot(InputValue v) { if (v.isPressed) Debug.Log("[Debug] Shoot"); }
    public void OnDrop(InputValue v)  { if (v.isPressed) Debug.Log("[Debug] Drop"); }
}