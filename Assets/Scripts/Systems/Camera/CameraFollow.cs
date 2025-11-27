using UnityEngine;

[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [Header("Configuración de Cancha")]
    public float midCourtX = 0f;
    public Vector3 leftHalfCenter;
    public Vector3 rightHalfCenter;

    [Header("Movimiento")]
    public float smooth = 2f;

    [Header("Zona Muerta")]
    public float deadZoneWidth = 1.5f; 
    private bool currentlyLeftSide = true;

    private PlayerController targetPlayer;

    void Start()
    {
        // Defaults si no están configurados
        if (leftHalfCenter == Vector3.zero)
            leftHalfCenter = new Vector3(-8f, 0f, transform.position.z);

        if (rightHalfCenter == Vector3.zero)
            rightHalfCenter = new Vector3(8f, 0f, transform.position.z);

        targetPlayer = PlayerController.CurrentControlled;

        // Determinar mitad inicial
        if (targetPlayer != null)
            currentlyLeftSide = targetPlayer.transform.position.x < midCourtX;

        // Posicionar cámara en mitad inicial
        Vector3 startPos = currentlyLeftSide ? leftHalfCenter : rightHalfCenter;
        startPos.z = transform.position.z;
        transform.position = startPos;
    }

    void LateUpdate()
    {
        targetPlayer = PlayerController.CurrentControlled;
        if (!targetPlayer) return;

        float px = targetPlayer.transform.position.x;

        // Márgenes de la zona muerta
        float leftThreshold = midCourtX - deadZoneWidth;
        float rightThreshold = midCourtX + deadZoneWidth;

        // Lógico: solo cambiar mitad si sale de la zona muerta
        if (currentlyLeftSide)
        {
            // Si estaba a la izquierda, solo cambiar si pasa de rightThreshold
            if (px > rightThreshold)
                currentlyLeftSide = false;
        }
        else
        {
            // Si estaba a la derecha, solo cambiar si pasa de leftThreshold
            if (px < leftThreshold)
                currentlyLeftSide = true;
        }

        // Elegir posición objetivo según la mitad actual
        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentlyLeftSide ? leftHalfCenter : rightHalfCenter;

        targetPos.z = currentPos.z;

        // Movimiento suave
        transform.position = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * smooth);
    }
}
