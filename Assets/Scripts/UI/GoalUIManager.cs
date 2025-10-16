// Assets/Scripts/UI/GoalUIManager.cs
using UnityEngine;
using System.Collections;

public class GoalUIManager : MonoBehaviour
{
    [Header("Texto/imagen GOOOOL dentro del Canvas")]
    public GameObject goalText;

    [Header("Efecto")]
    public float duration = 1.5f;
    public float shakeAmount = 15f;
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
            StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        goalText.SetActive(true);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            // temblor
            float x = Random.Range(-shakeAmount, shakeAmount);
            float y = Random.Range(-shakeAmount, shakeAmount);
            rect.anchoredPosition = originalPos + new Vector2(x, y);

            // pulso escala
            float s = Mathf.Lerp(startScale, endScale, Mathf.PingPong(t * 3f, 1f));
            rect.localScale = Vector3.one * s;

            yield return null;
        }

        rect.anchoredPosition = originalPos;
        rect.localScale = Vector3.one;
        goalText.SetActive(false);
    }
}