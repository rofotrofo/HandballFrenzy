using UnityEngine;
using System.Collections;

public class GoalUIManager : MonoBehaviour
{
    [Header("Referencia al texto GOOOL dentro del Canvas")]
    public GameObject goalText;

    [Header("Duración total del efecto (segundos)")]
    public float duration = 1.5f;

    [Header("Intensidad del temblor (px)")]
    public float shakeAmount = 15f;

    [Header("Escala inicial y final")]
    public float startScale = 1f;
    public float endScale = 1.3f;

    private RectTransform rect;
    private Vector2 originalPos;

    void Awake()
    {
        if (goalText != null)
        {
            rect = goalText.GetComponent<RectTransform>();
            originalPos = rect.anchoredPosition;
            goalText.SetActive(false);
        }
    }

    public void ShowGoal()
    {
        if (goalText != null)
            StartCoroutine(ShowGoalRoutine());
    }

    private IEnumerator ShowGoalRoutine()
    {
        goalText.SetActive(true);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Temblor aleatorio (Vector2)
            float x = Random.Range(-shakeAmount, shakeAmount);
            float y = Random.Range(-shakeAmount, shakeAmount);
            rect.anchoredPosition = originalPos + new Vector2(x, y);

            // Escala pulsante
            float scale = Mathf.Lerp(startScale, endScale, Mathf.PingPong(elapsed * 3f, 1f));
            rect.localScale = Vector3.one * scale;

            yield return null;
        }

        // Restaurar posición y escala
        rect.anchoredPosition = originalPos;
        rect.localScale = Vector3.one;
        goalText.SetActive(false);
    }
}