using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GoalTrigger : MonoBehaviour
{
    void Awake()
    {
        // Aseguramos que el collider de la portería sea un Trigger
        var col = GetComponent<Collider2D>();
        if (col && !col.isTrigger)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Buscamos si el objeto que entró tiene un BallController (puede estar en el padre)
        var ball = other.GetComponentInParent<BallController>();
        if (!ball) return;

        // Debug en consola
        Debug.Log("¡¡GOL!!");

        // Destruir la pelota
        Destroy(ball.gameObject);
    }
}