using UnityEngine;
using UnityEngine.UI;

public class SettingsButton : MonoBehaviour
{
    [SerializeField] private Button _openSettings;
    [SerializeField] private Button _closeSettings;
    [SerializeField] private Button[] _pauseButtons;

    private void Start()
    {
        _openSettings.onClick.AddListener(OpenSettingsScreen);
        _closeSettings.onClick.AddListener(CloseSettingsScreen);
    }

    private void OpenSettingsScreen()
    {
        UIManager.Source.OpenSettingsScreen();
        SetButtonInactive(false);
    }

    private void CloseSettingsScreen() 
    {
        SetButtonInactive(true);
        UIManager.Source.CloseSettingsScreen();
    }

    private void SetButtonInactive(bool state)
    {
        foreach (var button in _pauseButtons)
        {
            button.interactable = state;
        }
    }
}
