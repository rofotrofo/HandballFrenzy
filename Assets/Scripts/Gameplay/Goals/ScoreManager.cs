using System;
using UnityEngine;

[DefaultExecutionOrder(-100)] // Se inicializa antes que la UI
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Marcador")]
    public int homeGoals;    // Player
    public int visitorGoals; // IA

    public event Action<int,int> OnScoreChanged;

    [Header("Opcional")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        Emit();
    }

    public void ResetScore()
    {
        homeGoals = 0;
        visitorGoals = 0;
        Emit();
    }

    public void AddGoalHome()
    {
        homeGoals++;
        Emit();
    }

    public void AddGoalVisitor()
    {
        visitorGoals++;
        Emit();
    }

    public void AddGoal(TeamId teamWhoScored)
    {
        if (teamWhoScored == TeamId.Blue) AddGoalHome();     // asumiendo Blue=Player
        else                              AddGoalVisitor();  // Red=IA
    }

    private void Emit() => OnScoreChanged?.Invoke(homeGoals, visitorGoals);

#if UNITY_EDITOR
    // Debug rápido: H = gol Home, J = gol Visitor
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) AddGoalHome();
        if (Input.GetKeyDown(KeyCode.J)) AddGoalVisitor();
    }
#endif
}
