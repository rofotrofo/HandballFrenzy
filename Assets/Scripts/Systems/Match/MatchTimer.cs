using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MatchTimer : MonoBehaviour
{
    public static MatchTimer Instance { get; private set; }

    public static bool CountdownActive { get; private set; } = false;

    public static bool PendingNewMatch = false;

    [Header("Duraciones")]
    [Tooltip("Duración de CADA mitad en segundos (1:30 = 90)")]
    public float halfDurationSeconds = 90f;
    [Tooltip("Cuenta atrás al iniciar escena, tras gol y al iniciar 2H")]
    public float countdownSeconds = 3f;
    [Tooltip("Duración del banner de GOOOOL (si usas GoalUIManager aparte)")]
    public float goalBannerSeconds = 1.5f;

    [Header("HUD (Canvas actual)")]
    public TMP_Text clockText;        
    public TMP_Text halfText;        
    public GameObject countdownPanel; 
    public TMP_Text countdownText;
    public GameObject scorePanel;
    public GameObject pauseButton;
    public GameObject scoreUI;

    [Header("Fin de juego")]
    [Tooltip("Panel de Fin de Juego (desactivado por defecto) que quieres mostrar en Full Time.")]
    public GameObject endOfGamePanel;

    [Header("Botones en el Panel Final")]
    [SerializeField] private Button _nextMatchButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _resultText;
    [SerializeField] private TMP_Text _scoreText;

    [Header("Opcional: usar GoalUIManager si ya lo tienes")]
    public GoalUIManager goalUIManager;

    [Header("Configuración de Arenas")]
    [SerializeField] private TeamId _playerTeamId = TeamId.Blue; // Asume que Blue es el jugador
    private int _currentArenaIndex = 0; // 0: Wood, 1: Ice, 2: Sand

    // Estado
    private int currentHalf = 1;     
    private float timeLeft;           
    private bool running = false;     
    private bool handlingGoal = false;
    private bool fullTimeShown = false;
    private bool _isPaused = false;

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

        if (GameStateManager.Source != null)
        {
            GameStateManager.Source.OnGamePaused -= OnGamePaused;
            GameStateManager.Source.OnGameUnpaused -= OnGameUnpaused;
            GameStateManager.Source.OnGameStateChanged -= OnGameStateChanged;
        }
    }

    void Start()
    {
        ResetHalf(1);
        HideEndGamePanelIfAny();
        StartCoroutine(StartFlowWithCountdown());

        if (GameStateManager.Source != null)
        {
            GameStateManager.Source.OnGamePaused += OnGamePaused;
            GameStateManager.Source.OnGameUnpaused += OnGameUnpaused;
            GameStateManager.Source.OnGameStateChanged += OnGameStateChanged;
        }

        if (_nextMatchButton != null)
            _nextMatchButton.onClick.AddListener(StartNextMatch);

        if (_menuButton != null)
            _menuButton.onClick.AddListener(ReturnToMenu);
    }

    void Update()
    {
        if (!running || fullTimeShown || _isPaused) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft < 0f) timeLeft = 0f;

        UpdateClockUI();

        if (timeLeft <= 0f)
        {
            running = false;
            if (currentHalf == 1)
            {
                StartCoroutine(HalfFlow(2));
            }
            else
            {
                ShowFullTimePanelAndPause();
            }
        }
    }

    #region PAUSE LOGIC
    private void OnGamePaused()
    {
        _isPaused = true;
    }

    private void OnGameUnpaused()
    {
        _isPaused = false;
    }

    private void OnGameStateChanged(GameState newState)
    {
        if (newState == GameState.OnGameOver)
        {
            running = false;
            _isPaused = true;
        }
    }
    #endregion

    public void OnGoalScored()
    {
        if (handlingGoal || fullTimeShown || _isPaused) return;
        StartCoroutine(GoalFlow());
    }

    private IEnumerator StartFlowWithCountdown()
    {
        running = false;
        SetHudVisible(false);
        yield return CountdownCoroutine();
        SetHudVisible(true);
        pauseButton.SetActive(true);
        scorePanel.SetActive(true);
        scoreUI.SetActive(true);
        running = true;
    }

    private IEnumerator HalfFlow(int nextHalf)
    {
        handlingGoal = true;

        running = false;
        SetHudVisible(false);
        HideEndGamePanelIfAny();
        Time.timeScale = 1f;

        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        yield return null;
        ReconnectReferencesAfterSceneLoad();

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
        Time.timeScale = 1f;

        if (goalUIManager != null)
        {
            goalUIManager.ShowGoal();
            yield return new WaitForSeconds(goalBannerSeconds);
        }

        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        yield return null;

        ReconnectReferencesAfterSceneLoad();

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

        yield return new WaitForSeconds(0.25f);

        if (countdownPanel) countdownPanel.SetActive(false);
        CountdownActive = false;
    }

    private void ShowFullTimePanelAndPause()
    {
        fullTimeShown = true;
        CountdownActive = false;

        SetHudVisible(false);
        if (countdownPanel) countdownPanel.SetActive(false);

        if (endOfGamePanel != null)
        {
            ActivateOnlyThisPanel(endOfGamePanel);
            ShowGameResult();
        }
        else
        {
            Debug.LogWarning("MatchTimer: endOfGamePanel no asignado. Asigna tu panel final en el Inspector.");
        }

        Time.timeScale = 0f;
    }

    private void ShowGameResult()
    {
        if (ScoreManager.Instance == null) return;

        int playerScore = ScoreManager.Instance.homeGoals;
        int enemyScore = ScoreManager.Instance.visitorGoals;

        bool playerWon = playerScore > enemyScore;
        bool isDraw = playerScore == enemyScore;

        string resultMessage = "";
        if (playerWon)
            resultMessage = "VICTORY".Localize();
        else if (isDraw)
            resultMessage = "DRAW".Localize();
        else
            resultMessage = "LOSE".Localize();

        if (_resultText != null)
            _resultText.text = resultMessage;

        if (_scoreText != null)
            _scoreText.text = $"{playerScore} - {enemyScore}";

        if (_nextMatchButton != null)
        {
            bool showNextMatch = playerWon && (_currentArenaIndex < 2);
            _nextMatchButton.gameObject.SetActive(showNextMatch);

            if (showNextMatch)
            {
                string buttonText = (_currentArenaIndex == 1) ? "FINAL MATCH".Localize() : "NEXT MATCH".Localize();
                var textComp = _nextMatchButton.GetComponentInChildren<TMP_Text>();
                if (textComp != null)
                    textComp.text = buttonText;
            }
        }

        if (_menuButton != null)
            _menuButton.gameObject.SetActive(true);
    }

    private void StartNextMatch()
    {
        if (ArenaManager.Instance == null || _currentArenaIndex >= 2)
        {
            Debug.LogError("No se puede avanzar: ArenaManager no encontrado o es la última arena");
            return;
        }

        _currentArenaIndex++;

        ArenaManager.ArenaType nextArena = ArenaManager.ArenaType.Wood;
        switch (_currentArenaIndex)
        {
            case 0: nextArena = ArenaManager.ArenaType.Wood; break;
            case 1: nextArena = ArenaManager.ArenaType.Ice; break;
            case 2: nextArena = ArenaManager.ArenaType.Sand; break;
        }

        Debug.Log($"Cargando siguiente arena: {nextArena}");

        ArenaManager.Instance.startArena = nextArena;

        StartCoroutine(ReloadSceneForNewMatch());
    }

    private IEnumerator ReloadSceneForNewMatch()
    {
        fullTimeShown = false;
        _isPaused = false;

        HideEndGamePanelIfAny();

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScore();

        ResetHalf(1);
        running = false;

        if (GameStateManager.Source != null)
        {
            GameStateManager.Source.ChangeState(GameState.OnPlay);
        }

        Time.timeScale = 1f;

        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);

        while (!op.isDone) yield return null;

        yield return null;

        ReconnectReferencesAfterSceneLoad();
        StartCoroutine(StartFlowWithCountdown());
    }

    private void ReturnToMenu()
    {
        CleanupPersistentObjects();

        if (ArenaManager.Instance != null)
        {
            ArenaManager.Instance.ResetToInitialState();
        }

        SceneManager.LoadScene("MainMenu");
    }

    private void CleanupPersistentObjects()
    {
        if (Instance == this)
        {
            Instance = null;
            Destroy(gameObject);
        }

        if (ScoreManager.Instance != null)
        {
            var scoreObj = ScoreManager.Instance.gameObject;
            if (scoreObj.scene.buildIndex == -1)
                Destroy(scoreObj);
        }

        if (HudPersist.Instance != null)
        {
            Destroy(HudPersist.Instance.gameObject);
        }
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
        if (endOfGamePanel != null)
        {
            endOfGamePanel.SetActive(false);

            if (_nextMatchButton != null)
                _nextMatchButton.gameObject.SetActive(false);
            if (_menuButton != null)
                _menuButton.gameObject.SetActive(false);
        }
        fullTimeShown = false;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

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
        if (halfText) halfText.gameObject.SetActive(visible);
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (PendingNewMatch && s.name == SceneFlow.Game)
        {
            PendingNewMatch = false;
            BootNewMatchAfterLoad();
        }

        ReconnectReferencesAfterSceneLoad();

        UpdateHalfUI();
        UpdateClockUI();
    }

    private void ReconnectReferencesAfterSceneLoad()
    {
        var clockTextObj = GameObject.Find("ClockText");
        var halfTextObj = GameObject.Find("HalfText");
        var countdownPanelObj = GameObject.Find("CountdownPanel");
        var countdownTextObj = GameObject.Find("CountdownText");
        var endOfGamePanelObj = GameObject.Find("EndOfGamePanel");

        if (clockTextObj) clockText = clockTextObj.GetComponent<TMP_Text>();
        if (halfTextObj) halfText = halfTextObj.GetComponent<TMP_Text>();
        if (countdownPanelObj) countdownPanel = countdownPanelObj;
        if (countdownTextObj) countdownText = countdownTextObj.GetComponent<TMP_Text>();
        if (endOfGamePanelObj)
        {
            endOfGamePanel = endOfGamePanelObj;

            _nextMatchButton = endOfGamePanel.transform.Find("NextMatchButton")?.GetComponent<Button>();
            _menuButton = endOfGamePanel.transform.Find("MenuButton")?.GetComponent<Button>();
            _resultText = endOfGamePanel.transform.Find("ResultText")?.GetComponent<TMP_Text>();
            _scoreText = endOfGamePanel.transform.Find("ScoreText")?.GetComponent<TMP_Text>();

            if (_nextMatchButton != null)
            {
                _nextMatchButton.onClick.RemoveAllListeners();
                _nextMatchButton.onClick.AddListener(StartNextMatch);
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveAllListeners();
                _menuButton.onClick.AddListener(ReturnToMenu);
            }
        }
    }

    private void BootNewMatchAfterLoad()
    {
        _currentArenaIndex = 0;

        if (HudPersist.Instance != null)
            HudPersist.Instance.gameObject.SetActive(true);

        HideEndGamePanelIfAny();
        handlingGoal = false;
        fullTimeShown = false;

        ResetHalf(1);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScore();

        StopAllCoroutines();
        StartCoroutine(StartFlowWithCountdown());
    }
}
