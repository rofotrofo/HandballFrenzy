using UnityEngine;

public class MenuBootstrap : MonoBehaviour
{
    void Awake()
    {
        Time.timeScale = 1f;
        // Si tuvieras managers marcados con DontDestroyOnLoad por error, elimínalos aquí.
        // Por diseño, intenta NO usar DontDestroy en este pre-alpha.
    }
}