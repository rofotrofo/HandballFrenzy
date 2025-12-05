using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameStarter : MonoBehaviour
{
    private Button _startButton;

    private void Awake()
    {
        _startButton = GetComponent<Button>();
    }

    private void Start()
    {
        _startButton.onClick.AddListener(StartNewGame);
    }

    public void StartNewGame()
    {
        // Destruir objetos persistentes del juego anterior
        if (HudPersist.Instance != null)
        {
            Destroy(HudPersist.Instance.gameObject);
        }

        if (MatchTimer.Instance != null)
        {
            Destroy(MatchTimer.Instance.gameObject);
        }

        // Limpiar cualquier otro singleton persistente

        // Cargar la escena de juego
        SceneManager.LoadScene("Game");

        // Establecer la bandera para un nuevo partido
        MatchTimer.PendingNewMatch = true;
    }
}
