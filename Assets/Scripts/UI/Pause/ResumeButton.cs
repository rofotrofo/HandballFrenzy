using UnityEngine;
using UnityEngine.UI;

public class ResumeButton : MonoBehaviour
{
    private Button _resumeButton;

    private void Awake()
    {
        _resumeButton = GetComponent<Button>();
    }

    private void Start()
    {
        _resumeButton.onClick.AddListener(ResumeGame);
    }

    private void ResumeGame()
    {
        UIManager.Source.ClosePauseScreen();
        GameStateManager.Source.ChangeState(GameState.OnPlay);
    }
}
