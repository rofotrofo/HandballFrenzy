// Scripts/Gameplay/Goals/GoalTrigger.cs
using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [Tooltip("Si esta es la portería del equipo BLUE, entonces un gol aquí suma a RED.")]
    public TeamId belongsToTeam = TeamId.Blue;

    [Header("Reset")]
    public Transform kickoffPoint; // punto central para reanudar tras gol
    public float pauseAfterGoal = 1.0f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        // ¿Quién anota?
        TeamId scoringTeam = (belongsToTeam == TeamId.Blue) ? TeamId.Red : TeamId.Blue;
        ScoreManager.Instance.AddGoal(scoringTeam);

        // Reset rápido del balón
        StartCoroutine(GoalReset());
    }

    System.Collections.IEnumerator GoalReset()
    {
        // Pequeña pausa “cinemática”
        yield return new WaitForSeconds(pauseAfterGoal);

        var ball = BallController.Instance;
        if (!ball) yield break;

        ball.Drop();
        ball.transform.position = kickoffPoint ? kickoffPoint.position : Vector3.zero;

        var rb = ball.GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = Vector2.zero;
    }
}