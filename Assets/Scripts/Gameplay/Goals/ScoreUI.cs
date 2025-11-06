using UnityEngine;
using TMPro;
using System.Collections;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI homeScoreText;    // Player/Home
    [SerializeField] private TextMeshProUGUI visitorScoreText; // IA/Visitor

    private void OnEnable()
    {
        TrySubscribe();
        // Si aún no existe el ScoreManager, intenta suscribirte en cuanto aparezca
        if (ScoreManager.Instance == null) StartCoroutine(WaitAndSubscribe());
    }

    private void OnDisable()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    private void TrySubscribe()
    {
        if (ScoreManager.Instance == null) return;
        ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged; // evita doble sub
        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
        HandleScoreChanged(ScoreManager.Instance.homeGoals, ScoreManager.Instance.visitorGoals);
    }

    private System.Collections.IEnumerator WaitAndSubscribe()
    {
        // reintenta por si ScoreManager se crea un frame después
        for (int i = 0; i < 30; i++) // ~0.5s
        {
            if (ScoreManager.Instance != null) { TrySubscribe(); yield break; }
            yield return null;
        }
        Debug.LogWarning("No se encontró ScoreManager para ScoreUI.");
    }

    private void HandleScoreChanged(int home, int visitor)
    {
        if (homeScoreText)    homeScoreText.text = home.ToString();
        if (visitorScoreText) visitorScoreText.text = visitor.ToString();
    }
}