using UnityEngine;

[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    public Transform Target;
    public float smooth = 10f;

    void LateUpdate()
    {
        if (!Target) return;
        var pos = transform.position;
        pos = Vector3.Lerp(pos, new Vector3(Target.position.x, Target.position.y, pos.z), Time.deltaTime * smooth);
        transform.position = pos;
    }
}