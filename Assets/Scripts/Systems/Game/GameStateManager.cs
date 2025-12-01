using System;
using UnityEngine;

public class GameStateManager : Singleton<IStateSource>, IStateSource
{
    public GameState CurrentGameState { get; private set; }

    public event Action OnGamePaused;
    public event Action OnGameUnpaused;
    public event Action<GameState> OnGameStateChanged;

    private GameState _previousState;

    public void ChangeState(GameState state)
    {
        if (CurrentGameState == state) return;

        if (state == GameState.OnPause)
        {
            _previousState = CurrentGameState;
        }

        CurrentGameState = state;
        OnGameStateChanged?.Invoke(CurrentGameState);

        CheckPauseState(state);
    }

    public void ResumePreviousState()
    {
        ChangeState(_previousState);
    }

    private void CheckPauseState(GameState state)
    {
        if (state == GameState.OnPause || state == GameState.OnGameOver)
        {
            OnGamePaused?.Invoke();
        }
        else
        {
            OnGameUnpaused?.Invoke();
        }
    }
}

public enum GameState
{
    OnPlay,
    OnPause,
    OnGameOver
}
