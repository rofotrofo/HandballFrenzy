using UnityEngine;

public enum ArenaType { Wood, Sand, Ice }

public class ArenaManager : MonoBehaviour
{
    [Header("Arena roots (activa solo una)")]
    public GameObject woodArenaRoot;  // duela (normal)
    public GameObject sandArenaRoot;  // arena (SurfaceZone2D)
    public GameObject iceArenaRoot;   // hielo (IceZone2D)

    [Header("UI (opcional)")]
    public TMPro.TextMeshProUGUI arenaLabel;

    [Header("Start Arena")]
    public ArenaType startArena = ArenaType.Wood;

    void Start() => SetArena(startArena);

    public void SetArena(ArenaType type)
    {
        if (woodArenaRoot) woodArenaRoot.SetActive(type == ArenaType.Wood);
        if (sandArenaRoot) sandArenaRoot.SetActive(type == ArenaType.Sand);
        if (iceArenaRoot)  iceArenaRoot.SetActive(type == ArenaType.Ice);

        if (arenaLabel)
            arenaLabel.text = type switch
            {
                ArenaType.Wood => "Duela",
                ArenaType.Sand => "Arena",
                ArenaType.Ice  => "Hielo",
                _ => ""
            };
    }

    // Llamable desde botones para probar
    public void SetWood() => SetArena(ArenaType.Wood);
    public void SetSand() => SetArena(ArenaType.Sand);
    public void SetIce()  => SetArena(ArenaType.Ice);
}