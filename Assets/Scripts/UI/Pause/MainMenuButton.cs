using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuButton : MonoBehaviour
{
    private Button _mainMenuButton;

    private void Awake()
    {
        _mainMenuButton = GetComponent<Button>();
    }
    private void Start()
    {
        _mainMenuButton.onClick.AddListener(MainMenu);
    }

    private void MainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
