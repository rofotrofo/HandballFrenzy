using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    // llamado por el botón "Jugar"
    public void PlayGame()
    {
        SceneFlow.LoadGame();
    }

    // llamado por el botón "Salir"
    public void QuitGame()
    {
        SceneFlow.Quit();
    }

    // llamado por el botón "Volver al Menú Principal" (desde el juego)
    public void BackToMenu()
    {
        SceneFlow.LoadMenu();
    }
}