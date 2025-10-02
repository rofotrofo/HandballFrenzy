// Scripts/Gameplay/Goals/ScoreManager.cs
using UnityEngine;
using System;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int blueGoals { get; private set; }
    public int redGoals { get; private set; }

    public event Action<int,int> OnScoreChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // opcional si cambias de escena
    }

    public void AddGoal(TeamId team)
    {
        if (team == TeamId.Blue) blueGoals++;
        else redGoals++;

        OnScoreChanged?.Invoke(blueGoals, redGoals);
    }

    public void ResetScore()
    {
        blueGoals = redGoals = 0;
        OnScoreChanged?.Invoke(blueGoals, redGoals);
    }
}