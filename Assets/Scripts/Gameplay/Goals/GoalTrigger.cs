using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class GoalTrigger : MonoBehaviour
{
    [Header("Refs")]
    public BallSpawner spawner;

    [Header("Timings (seconds)")]
    public float respawnDelay = 1.5f;

    bool busy;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col && !col.isTrigger)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // La pelota puede entrar con su collider trigger (hijo). Buscamos el BallController en el padre.
        var ball = other.GetComponentInParent<BallController>();
        if (!ball || busy) return;

        StartCoroutine(HandleGoal(ball));
    }

    IEnumerator HandleGoal(BallController ball)
    {
        busy = true;

        // 1) Debug en consola
        Debug.Log("¡¡GOL!!");

        // 2) Destruir pelota actual
        if (spawner) spawner.Despawn(ball);
        else Destroy(ball.gameObject);

        // 3) Esperar y respawnear
        yield return new WaitForSeconds(respawnDelay);
        if (spawner) spawner.SpawnBall();

        busy = false;
    }
}