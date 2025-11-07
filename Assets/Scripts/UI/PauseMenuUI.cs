using UnityEngine;

public class PauseMenuUI : MonoBehaviour
{
    public GameObject pauseMenuUI; // Asigna aquí el panel del menú de pausa desde el inspector
    private bool isPaused = false;

    // Función llamada por el botón de pausa
    public void PauseGame()
    {
        if (isPaused) return;

        pauseMenuUI.SetActive(true);      // Muestra el menú de pausa
        Time.timeScale = 0f;              // Pausa el tiempo del juego
        isPaused = true;
    }

    // Función llamada por el botón de "Reanudar" o "Salir del menú de pausa"
    public void ResumeGame()
    {
        if (!isPaused) return;

        pauseMenuUI.SetActive(false);     // Oculta el menú de pausa
        Time.timeScale = 1f;              // Reanuda el tiempo del juego
        isPaused = false;
    }
}