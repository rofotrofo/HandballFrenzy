using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SceneReloader : MonoBehaviour
{
    // Llama esto para reiniciar TODO el layout de la escena (como al arrancar).
    public void RestartScene(bool keepHud = false)
    {
        if (keepHud && HudPersist.Instance != null)
            HudPersist.Instance.ArmPreserve(); // mantén HUD vivo

        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    // Azúcar sintáctico por si lo llamas desde botones / eventos
    public void RestartLayoutOnly() => RestartScene(false);
    public void RestartKeepingHUD()  => RestartScene(true);
}