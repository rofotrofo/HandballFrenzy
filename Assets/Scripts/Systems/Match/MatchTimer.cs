using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class MatchTimer : MonoBehaviour
{
    public static MatchTimer Instance { get; private set; }

    // 👉 Flag global para bloquear movimiento durante el countdown
    public static bool CountdownActive { get; private set; } = false;

    [Header("Duraciones")]
    [Tooltip("Duración de CADA mitad en segundos (1:30 = 90)")]
    public float halfDurationSeconds = 90f;
    [Tooltip("Cuenta atrás al iniciar escena y tras gol")]
    public float countdownSeconds = 3f;
    [Tooltip("Duración del banner de GOOOOL (si usas GoalUIManager aparte)")]
    public float goalBannerSeconds = 1.5f;

    [Header("UI (asigna en el Canvas de la escena)")]
    public TMP_Text clockText;        // mm:ss
    public TMP_Text halfText;         // "1H" / "2H"
    public GameObject countdownPanel; // panel centrado
    public TMP_Text countdownText;    // 3..2..1..

    [Header("Opcional: usar GoalUIManager si ya lo tienes")]
    public GoalUIManager goalUIManager;

    // Estado
    private int currentHalf = 1;      // 1 o 2
    private float timeLeft;           // tiempo restante de la mitad
    private bool running = false;     // el reloj corre
    private bool handlingGoal = false;

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
        // Arranque: mitad 1 y countdown inicial
        ResetHalf(1);
        StartCoroutine(StartFlowWithCountdown());
    }

    void Update()
    {
        if (!running) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft < 0f) timeLeft = 0f;

        UpdateClockUI();

        if (timeLeft <= 0f)
        {
            running = false;
            if (currentHalf == 1)
            {
                // Cambio a segunda mitad con countdown
                StartCoroutine(SwitchHalfWithCountdown(2));
            }
            else
            {
                // Partido terminado (puedes mostrar "Full Time" aquí si quieres)
            }
        }
    }

    // ================= API =================

    public void OnGoalScored()
    {
        if (handlingGoal) return;
        StartCoroutine(GoalFlow());
    }

    // =============== Flujos ===============

    private IEnumerator StartFlowWithCountdown()
    {
        running = false;
        SetHudVisible(false);
        yield return CountdownCoroutine(); // CountdownActive true durante el conteo
        SetHudVisible(true);
        running = true;
    }

    private IEnumerator SwitchHalfWithCountdown(int nextHalf)
    {
        currentHalf = nextHalf;
        ResetHalf(currentHalf);
        UpdateHalfUI();

        running = false;
        SetHudVisible(false);
        yield return CountdownCoroutine();
        SetHudVisible(true);
        running = true;
    }

    private IEnumerator GoalFlow()
    {
        handlingGoal = true;

        // 1) Pausar y ocultar HUD
        running = false;
        SetHudVisible(false);

        // 2) Mostrar banner de GOOL (si está asignado)
        if (goalUIManager != null)
        {
            goalUIManager.ShowGoal();
            yield return new WaitForSeconds(goalBannerSeconds);
        }

        // 3) Recargar escena para reiniciar layout (pelota, posiciones, etc.)
        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        // 4) Countdown post-gol (bloquea movimiento)
        yield return CountdownCoroutine();

        // 5) Mostrar HUD y reanudar reloj
        SetHudVisible(true);
        running = true;
        handlingGoal = false;
    }

    private IEnumerator CountdownCoroutine()
    {
        // Activar panel + bloquear movimiento globalmente
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

        // Pequeño colchón visual para el "1"
        yield return new WaitForSeconds(0.25f);

        // Desactivar panel + liberar movimiento
        if (countdownPanel) countdownPanel.SetActive(false);
        CountdownActive = false;
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

    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string Game     = "Game";

        public static void LoadMenu()
        {
            CleanupHUD();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(MainMenu, LoadSceneMode.Single);
        }

        public static void LoadGame()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(Game, LoadSceneMode.Single);
        }

        static void CleanupHUD()
        {
            var hud = HudPersist.Instance;
            if (hud != null)
            {
                hud.gameObject.SetActive(false);
                // O destrúyelo si tu menú tiene su propia UI:
                // Object.Destroy(hud.gameObject);
            }
        }
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Si usas un Canvas nuevo por escena, vuelve a enganchar referencias aquí (opcional).
        UpdateHalfUI();
        UpdateClockUI();
    }
}
