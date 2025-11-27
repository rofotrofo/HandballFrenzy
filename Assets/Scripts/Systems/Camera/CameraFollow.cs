using UnityEngine;

[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [Header("Configuración de Cancha")]
    public float midCourtX = 0f;     // Línea que divide la cancha en 2 mitades
    public Vector3 leftHalfCenter;   // Centro de la mitad izquierda
    public Vector3 rightHalfCenter;  // Centro de la mitad derecha

    [Header("Movimiento")]
    public float smooth = 2f;

    private Transform ball;

    void Start()
    {
        ball = BallController.Instance.transform;

        // Valores por default si no los pones en el inspector
        if (leftHalfCenter == Vector3.zero)
            leftHalfCenter = new Vector3(-8f, 0f, transform.position.z);

        if (rightHalfCenter == Vector3.zero)
            rightHalfCenter = new Vector3(8f, 0f, transform.position.z);
    }

    void LateUpdate()
    {
        if (!ball) return;

        Vector3 currentPos = transform.position;
        Vector3 targetPos;

        // ¿En qué mitad está la pelota?
        if (ball.position.x < midCourtX)
        {
            // MITAD IZQUIERDA
            targetPos = leftHalfCenter;
        }
        else
        {
            // MITAD DERECHA
            targetPos = rightHalfCenter;
        }

        // Mantener Z
        targetPos.z = currentPos.z;

        // Movimiento suave tipo “arena deportiva”
        transform.position = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * smooth);
    }
}