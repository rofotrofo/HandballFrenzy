using UnityEngine;
using UnityEngine.UI;

public class PauseButton : MonoBehaviour
{
    private Button _pauseButton;

    private void Awake()
    {
        _pauseButton = GetComponent<Button>();
    }

    private void Start()
    {
        _pauseButton.onClick.AddListener(PauseGame);
    }

    private void PauseGame()
    {
        UIManager.Source.OpenPauseScreen();
        GameStateManager.Source.ChangeState(GameState.OnPause);
    }
}
