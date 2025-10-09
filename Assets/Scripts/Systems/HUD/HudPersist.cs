using UnityEngine;

[DisallowMultipleComponent]
public class HudPersist : MonoBehaviour
{
    public static HudPersist Instance { get; private set; }

    // Marca interna: si la próxima recarga debe conservar este HUD
    private bool _preserveArmed = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Si ya hay uno vivo, nos destruimos para evitar duplicados
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // HUD persiste entre escenas
    }

    /// Llama esto ANTES de reiniciar la escena para conservar el HUD.
    public void ArmPreserve()
    {
        _preserveArmed = true;
    }

    void OnEnable()
    {
        // Si NO está armado, significa que esta carga viene desde el editor (Play)
        // o un reinicio "duro": permitimos que exista sólo uno.
    }

    void OnSceneLoaded()
    {
        // Gancho opcional si en el futuro quieres re-vincular señales del HUD
        // con nuevos objetos de la escena. Por ahora no es necesario.
    }
}
