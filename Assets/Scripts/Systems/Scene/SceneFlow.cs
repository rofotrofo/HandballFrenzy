using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneFlow
{
    // Pon los nombres EXACTOS como están en Build Settings
    public const string MainMenu = "MainMenu";
    public const string Game     = "Game";

    /// Carga el menú principal (estado fresco).
    public static void LoadMenu()
    {
        CleanupStatics();
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenu, LoadSceneMode.Single);
    }

    /// Carga la escena del juego (estado fresco).
    public static void LoadGame()
    {
        CleanupStatics();
        Time.timeScale = 1f;
        SceneManager.LoadScene(Game, LoadSceneMode.Single);
    }

    /// Salir del juego (funciona en build; en editor, detiene Play).
    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// Punto único para limpiar estáticos/singletons si alguno quedara colgado.
    static void CleanupStatics()
    {
        // Ejemplos: si tus singletons usan .Instance estática, asegúrate que OnDestroy las ponga en null.
        // Aquí puedes forzar limpiezas si hiciera falta:
        // BallController.Instance = null;   // NO necesario si tu OnDestroy ya lo hace.
        // TeamRegistry.Clear();             // Si tienes un registro estático, límpialo aquí.
        // Cualquier otro cache/estado global que quieras resetear al cambiar de escena.
    }
}