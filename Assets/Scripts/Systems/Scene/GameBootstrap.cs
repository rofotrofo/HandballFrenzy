using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        Time.timeScale = 1f;

        // Si requieres “semillas” de estado inicial, hazlo aquí:
        // - Reposicionar jugadores
        // - Resetear powerups/spawners
        // - Reiniciar lógica de mitad/cronómetro si (y sólo si) no quieres persistir HUD
        //
        // Si más adelante decides mantener HUD/score entre cargas,
        // ese HUD vivirá en DontDestroy y NO se reseteará aquí.
    }
}