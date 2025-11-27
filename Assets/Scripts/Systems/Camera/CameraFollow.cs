using UnityEngine;

[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public float smooth = 5f; 
    public Vector2 deadZoneSize = new Vector2(3f, 2f); 
    // X = ancho de zona muerta | Y = alto

    private PlayerController targetPlayer;
    private Transform cam;

    void Start()
    {
        cam = transform;
        targetPlayer = PlayerController.CurrentControlled;

        if (targetPlayer != null)
        {
            Vector3 startPos = targetPlayer.transform.position;
            startPos.z = cam.position.z;
            cam.position = startPos;
        }
    }

    void LateUpdate()
    {
        targetPlayer = PlayerController.CurrentControlled;
        if (!targetPlayer) return;

        Vector3 camPos = cam.position;
        Vector3 playerPos = targetPlayer.transform.position;

        // Mantener Z
        playerPos.z = camPos.z;

        // === Zona muerta ===
        float dx = playerPos.x - camPos.x;
        float dy = playerPos.y - camPos.y;

        bool outX = Mathf.Abs(dx) > deadZoneSize.x;
        bool outY = Mathf.Abs(dy) > deadZoneSize.y;

        Vector3 targetPos = camPos;

        // Solo mover cámara si el jugador sale del rectángulo muerto
        if (outX) targetPos.x = playerPos.x - Mathf.Sign(dx) * deadZoneSize.x;
        if (outY) targetPos.y = playerPos.y - Mathf.Sign(dy) * deadZoneSize.y;

        // Movimiento suave
        cam.position = Vector3.Lerp(camPos, targetPos, Time.deltaTime * smooth);
    }
}