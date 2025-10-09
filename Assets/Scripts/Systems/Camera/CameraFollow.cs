using UnityEngine;

[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [Header("Configuración")]
    public Transform Target;
    public float smooth = 10f;

    void Awake()
    {
        // Intentar engancharse al BallController (si existe)
        if (BallController.Instance)
            BallController.Instance.OnOwnerChanged += OnBallOwnerChanged;
    }

    void OnDestroy()
    {
        if (BallController.Instance)
            BallController.Instance.OnOwnerChanged -= OnBallOwnerChanged;
    }

    void LateUpdate()
    {
        if (!Target) return;

        Vector3 pos = transform.position;
        Vector3 targetPos = new Vector3(Target.position.x, Target.position.y, pos.z);
        transform.position = Vector3.Lerp(pos, targetPos, Time.deltaTime * smooth);
    }

    private void OnBallOwnerChanged(PlayerController newOwner)
    {
        // Si la pelota no tiene dueño (Drop), no seguir a nadie.
        Target = newOwner ? newOwner.transform : BallController.Instance.transform;
    }
}