using UnityEngine;

public class ParticleController : MonoBehaviour
{
    [SerializeField] private ParticleSystem iceParticles;
    [SerializeField] private ParticleSystem sandParticles;

    void Start()
    {
        SetParticlesActive(false, false);
        SubscribeToEvents();
        UpdateForCurrentArena();
    }

    void OnEnable()
    {
        SubscribeToEvents();
        UpdateForCurrentArena();
    }

    void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (ArenaManager.Instance != null)
        {
            ArenaManager.Instance.OnArenaChanged += HandleArenaChanged;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (ArenaManager.Instance != null)
        {
            ArenaManager.Instance.OnArenaChanged -= HandleArenaChanged;
        }
    }

    private void HandleArenaChanged(ArenaManager.ArenaType newArena)
    {
        UpdateParticles(newArena);
    }

    private void UpdateForCurrentArena()
    {
        if (ArenaManager.Instance != null)
        {
            UpdateParticles(ArenaManager.Instance.GetCurrentArenaType());
        }
    }

    private void UpdateParticles(ArenaManager.ArenaType arena)
    {
        switch (arena)
        {
            case ArenaManager.ArenaType.Ice:
                SetParticlesActive(true, false);
                break;

            case ArenaManager.ArenaType.Sand:
                SetParticlesActive(false, true);
                break;

            case ArenaManager.ArenaType.Wood:
            default:
                SetParticlesActive(false, false);
                break;
        }
    }

    private void SetParticlesActive(bool iceActive, bool sandActive)
    {
        if (iceParticles != null)
        {
            iceParticles.gameObject.SetActive(iceActive);
            if (iceActive && !iceParticles.isPlaying)
                iceParticles.Play();
            else if (!iceActive && iceParticles.isPlaying)
                iceParticles.Stop();
        }

        if (sandParticles != null)
        {
            sandParticles.gameObject.SetActive(sandActive);
            if (sandActive && !sandParticles.isPlaying)
                sandParticles.Play();
            else if (!sandActive && sandParticles.isPlaying)
                sandParticles.Stop();
        }
    }
}
