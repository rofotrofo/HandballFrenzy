using UnityEngine;
using TMPro;

public class ScoreUI : MonoBehaviour
{
    [Header("Referencias a los textos del marcador")]
    [SerializeField] private TextMeshProUGUI homeScoreText;     // Goles del jugador
    [SerializeField] private TextMeshProUGUI visitorScoreText;  // Goles del IA

    private void OnEnable()
    {
        if (ScoreManager.Instance != null)
        {
            // Suscribirse al evento de cambio de marcador
            ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;

            // Mostrar el marcador actual al iniciar
            HandleScoreChanged(ScoreManager.Instance.blueGoals, ScoreManager.Instance.redGoals);
        }
    }

    private void OnDisable()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int blueGoals, int redGoals)
    {
        // Suponiendo que Blue = jugador (home) y Red = IA (visitor)
        if (homeScoreText != null)
            homeScoreText.text = blueGoals.ToString();

        if (visitorScoreText != null)
            visitorScoreText.text = redGoals.ToString();
    }
}