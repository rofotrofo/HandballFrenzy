using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("Prefab & Spawn Point")]
    public BallController ballPrefab;
    public Transform spawnPoint;

    private BallController currentBall;

    void Start()
    {
        // Instancia inicial al comenzar
        if (ballPrefab) SpawnBall();
    }

    public BallController SpawnBall()
    {
        if (!ballPrefab) 
        {
            Debug.LogError("[BallSpawner] No se asignó el prefab de la pelota.");
            return null;
        }

        Vector3 pos = spawnPoint ? spawnPoint.position : transform.position;
        currentBall = Instantiate(ballPrefab, pos, Quaternion.identity);
        return currentBall;
    }

    public void Despawn(BallController ball)
    {
        if (!ball) return;
        if (currentBall == ball) currentBall = null;
        Destroy(ball.gameObject);
    }
}