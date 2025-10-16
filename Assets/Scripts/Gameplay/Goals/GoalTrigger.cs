using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [Header("Opcional")]
    [Tooltip("Si está activo, destruye la pelota al cruzar la línea de gol.")]
    [SerializeField] private bool destroyBallOnContact = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var ball = other.GetComponentInParent<BallController>();
        if (!ball) return;

        if (destroyBallOnContact)
            Destroy(ball.gameObject);

        // Flujo principal: delega en el MatchTimer
        if (MatchTimer.Instance != null)
        {
            MatchTimer.Instance.OnGoalScored();
            return;
        }

        // Fallback suave (por si aún no integras el MatchTimer):
        var goalUI = Object.FindFirstObjectByType<GoalUIManager>();
        if (goalUI != null)
            goalUI.ShowGoal();
        // (En el flujo nuevo ya NO recargamos escena aquí; lo hace el MatchTimer)
    }
}