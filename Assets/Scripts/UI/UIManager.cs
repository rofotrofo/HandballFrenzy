using UnityEngine;
using UnityEngine.UI;

public class UIManager : Singleton<IUISource>, IUISource
{
    [SerializeField] private GameObject _pauseCanvas;
    [SerializeField] private GameObject _settingsCanvas;

    private void Start()
    {
        if (GameStateManager.Source != null)
        {
            GameStateManager.Source.OnGamePaused += OpenPauseScreen;
            GameStateManager.Source.OnGameUnpaused += HandleGameUnpaused;
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Source != null)
        {
            GameStateManager.Source.OnGamePaused -= OpenPauseScreen;
            GameStateManager.Source.OnGameUnpaused -= HandleGameUnpaused;
        }
    }

    private void HandleGameUnpaused()
    {
        ClosePauseScreen();
        CloseSettingsScreen();
    }

    public void OpenPauseScreen()
    {
        _pauseCanvas.SetActive(true);
    }

    public void ClosePauseScreen()
    {
        _pauseCanvas.SetActive(false);
    }

    public void OpenSettingsScreen()
    {
        _settingsCanvas.SetActive(true);
    }

    public void CloseSettingsScreen()
    {
        _settingsCanvas.SetActive(false);
    }
}

public interface IUISource
{
    void OpenPauseScreen();
    void ClosePauseScreen();
    void OpenSettingsScreen();
    void CloseSettingsScreen();
}
