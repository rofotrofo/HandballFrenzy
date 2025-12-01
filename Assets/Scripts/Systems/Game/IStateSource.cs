using System;

public interface IStateSource
{
    event Action<GameState> OnGameStateChanged;
    event Action OnGamePaused;
    event Action OnGameUnpaused;
    GameState CurrentGameState { get; }
    void ChangeState(GameState state);
    void ResumePreviousState();
}
