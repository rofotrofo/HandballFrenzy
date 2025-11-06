using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GoalTrigger : MonoBehaviour
{
    [Header("¿Quién defiende ESTA portería?")]
    [SerializeField] private TeamId ownerTeam = TeamId.Red;

    [Header("Opcional")]
    [Tooltip("Si está activo, destruye la pelota al cruzar la línea de gol.")]
    [SerializeField] private bool destroyBallOnContact = true;

    // Flag para evitar contar doble
    private bool goalRegistered = false;

    private TeamId Opponent(TeamId t) => (t == TeamId.Blue) ? TeamId.Red : TeamId.Blue;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Asegura que fue la pelota
        var ball = other.GetComponentInParent<BallController>();
        if (!ball) return;

        // Evita doble conteo
        if (goalRegistered) return;
        goalRegistered = true;

        // Determina quién anotó (el contrario al dueño de la portería)
        TeamId scoringTeam = Opponent(ownerTeam);

        // Suma el gol en el marcador
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddGoal(scoringTeam);

        // Notifica al MatchTimer / flujo de gol, si usas uno
        if (MatchTimer.Instance != null)
        {
            // Si tu MatchTimer ya tiene esta firma, úsala:
            // MatchTimer.Instance.OnGoalScored(scoringTeam);

            // Si tu MatchTimer sólo tiene OnGoalScored() sin parámetros (como en tu versión),
            // déjalo así, y el MatchTimer no sabrá quién anotó (pero el ScoreManager sí):
            MatchTimer.Instance.OnGoalScored();
        }
        else
        {
            // Fallback suave si aún no integras MatchTimer:
            var goalUI = Object.FindFirstObjectByType<GoalUIManager>();
            if (goalUI != null) goalUI.ShowGoal();
        }

        // Opcional: destruir pelota (si tu flujo así lo prefiere)
        if (destroyBallOnContact)
            Destroy(ball.gameObject);

        // Reactiva el trigger después de un pequeño delay
        Invoke(nameof(ResetGoalFlag), 0.5f);
    }

    private void ResetGoalFlag()
    {
        goalRegistered = false;
    }
}
