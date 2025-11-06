using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class MatchTimer : MonoBehaviour
{
    public static MatchTimer Instance { get; private set; }

    public static bool CountdownActive { get; private set; } = false;

    // Bandera para arrancar un partido “desde cero” al cargar la escena de juego
    public static bool PendingNewMatch = false;

    [Header("Duraciones")]
    [Tooltip("Duración de CADA mitad en segundos (1:30 = 90)")]
    public float halfDurationSeconds = 90f;
    [Tooltip("Cuenta atrás al iniciar escena, tras gol y al iniciar 2H")]
    public float countdownSeconds = 3f;
    [Tooltip("Duración del banner de GOOOOL (si usas GoalUIManager aparte)")]
    public float goalBannerSeconds = 1.5f;

    [Header("HUD (Canvas actual)")]
    public TMP_Text clockText;        // mm:ss
    public TMP_Text halfText;         // "1H" / "2H"
    public GameObject countdownPanel; // panel centrado
    public TMP_Text countdownText;    // 3..2..1..

    [Header("Fin de juego")]
    [Tooltip("Panel de Fin de Juego (desactivado por defecto) que quieres mostrar en Full Time.")]
    public GameObject endOfGamePanel;

    [Header("Opcional: usar GoalUIManager si ya lo tienes")]
    public GoalUIManager goalUIManager;

    // Estado
    private int currentHalf = 1;      // 1 o 2
    private float timeLeft;           // tiempo restante de la mitad
    private bool running = false;     // el reloj corre
    private bool handlingGoal = false;
    private bool fullTimeShown = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // Primer arranque (si la escena de juego abre con este objeto presente)
        ResetHalf(1);
        HideEndGamePanelIfAny();
        StartCoroutine(StartFlowWithCountdown());
    }

    void Update()
    {
        if (!running || fullTimeShown) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft < 0f) timeLeft = 0f;

        UpdateClockUI();

        if (timeLeft <= 0f)
        {
            running = false;
            if (currentHalf == 1)
            {
                // Fin 1H → recarga y arranca 2H con countdown
                StartCoroutine(HalfFlow(2));
            }
            else
            {
                // FULL TIME → pausa y panel final
                ShowFullTimePanelAndPause();
            }
        }
    }

    // ================= API =================

    public void OnGoalScored()
    {
        if (handlingGoal || fullTimeShown) return;
        StartCoroutine(GoalFlow());
    }

    // =============== Flujos ===============

    private IEnumerator StartFlowWithCountdown()
    {
        running = false;
        SetHudVisible(false);
        yield return CountdownCoroutine();
        SetHudVisible(true);
        running = true;
    }

    /// Flujo al terminar una mitad (recarga escena, arranca siguiente mitad).
    private IEnumerator HalfFlow(int nextHalf)
    {
        handlingGoal = true;

        running = false;
        SetHudVisible(false);
        HideEndGamePanelIfAny();
        Time.timeScale = 1f; // por si acaso

        // Recarga la misma escena para resetear posiciones/bola
        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        ResetHalf(nextHalf);
        UpdateHalfUI();

        yield return CountdownCoroutine();

        SetHudVisible(true);
        running = true;
        handlingGoal = false;
    }

    private IEnumerator GoalFlow()
    {
        handlingGoal = true;

        running = false;
        SetHudVisible(false);
        HideEndGamePanelIfAny();
        Time.timeScale = 1f; // por si acaso

        if (goalUIManager != null)
        {
            goalUIManager.ShowGoal();
            yield return new WaitForSeconds(goalBannerSeconds);
        }

        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        yield return CountdownCoroutine();

        SetHudVisible(true);
        running = true;
        handlingGoal = false;
    }

    private IEnumerator CountdownCoroutine()
    {
        CountdownActive = true;
        if (countdownPanel) countdownPanel.SetActive(true);

        float t = countdownSeconds;
        while (t > 0f)
        {
            int shown = Mathf.CeilToInt(t);
            if (countdownText) countdownText.text = shown.ToString();
            t -= Time.deltaTime;
            yield return null;
        }

        // Pequeño colchón para que el "1" se lea
        yield return new WaitForSeconds(0.25f);

        if (countdownPanel) countdownPanel.SetActive(false);
        CountdownActive = false;
    }

    // ============== FULL TIME ==============

    private void ShowFullTimePanelAndPause()
    {
        fullTimeShown = true;
        CountdownActive = false;

        SetHudVisible(false);
        if (countdownPanel) countdownPanel.SetActive(false);

        if (endOfGamePanel != null)
        {
            ActivateOnlyThisPanel(endOfGamePanel);
        }
        else
        {
            Debug.LogWarning("MatchTimer: endOfGamePanel no asignado. Asigna tu panel final en el Inspector.");
        }

        Time.timeScale = 0f;
    }

    private void ActivateOnlyThisPanel(GameObject panel)
    {
        if (panel == null) return;

        Transform parent = panel.transform.parent;
        if (parent == null) { panel.SetActive(true); return; }

        for (int i = 0; i < parent.childCount; i++)
            parent.GetChild(i).gameObject.SetActive(false);

        panel.SetActive(true);
    }

    private void HideEndGamePanelIfAny()
    {
        if (endOfGamePanel != null) endOfGamePanel.SetActive(false);
        fullTimeShown = false;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    // ============== Helpers ==============

    private void ResetHalf(int halfIndex)
    {
        currentHalf = Mathf.Clamp(halfIndex, 1, 2);
        timeLeft = halfDurationSeconds;
        UpdateHalfUI();
        UpdateClockUI();
    }

    private void UpdateClockUI()
    {
        if (!clockText) return;
        int sec = Mathf.CeilToInt(timeLeft);
        int m = sec / 60;
        int s = sec % 60;
        clockText.text = $"{m:0}:{s:00}";
    }

    private void UpdateHalfUI()
    {
        if (!halfText) return;
        halfText.text = (currentHalf == 1) ? "1H" : "2H";
    }

    private void SetHudVisible(bool visible)
    {
        if (clockText) clockText.gameObject.SetActive(visible);
        if (halfText)  halfText.gameObject.SetActive(visible);
    }

    // ============== Escena cargada ==============

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Si venimos del Menú y acabamos de cargar la escena de juego, boot limpio
        if (PendingNewMatch && s.name == SceneFlow.Game)
        {
            PendingNewMatch = false;
            BootNewMatchAfterLoad();
        }

        // Asegura textos si recargamos por gol/mitad
        UpdateHalfUI();
        UpdateClockUI();
    }

    private void BootNewMatchAfterLoad()
    {
        // Re-activa HUD persistente si lo apagas en el menú
        if (HudPersist.Instance != null)
            HudPersist.Instance.gameObject.SetActive(true);

        // Limpia estado y marcador
        HideEndGamePanelIfAny();
        handlingGoal = false;
        fullTimeShown = false;

        ResetHalf(1);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScore();

        // Arranca countdown inicial del partido
        StopAllCoroutines();
        StartCoroutine(StartFlowWithCountdown());
    }
}
