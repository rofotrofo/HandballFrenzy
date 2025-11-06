using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneFlow
{
    // Nombres EXACTOS como en Build Settings
    public const string MainMenu = "MainMenu";
    public const string Game     = "Game";

    /// Ir al menú (estado fresco).
    public static void LoadMenu()
    {
        // No queremos boot automático de partido al cargar menú
        if (MatchTimer.Instance != null) MatchTimer.PendingNewMatch = false;

        // Opcional: apaga HUD persistente para que el menú quede limpio
        if (HudPersist.Instance != null)
            HudPersist.Instance.gameObject.SetActive(false);

        // Opcional: reset de marcador al salir al menú
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScore();

        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenu, LoadSceneMode.Single);
    }

    /// Ir al juego (boot limpio al cargar la escena).
    public static void LoadGame()
    {
        // Señal para que MatchTimer inicialice TODO al entrar a la escena
        MatchTimer.PendingNewMatch = true;

        // Nos aseguramos de no llegar pausados
        Time.timeScale = 1f;

        SceneManager.LoadScene(Game, LoadSceneMode.Single);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}