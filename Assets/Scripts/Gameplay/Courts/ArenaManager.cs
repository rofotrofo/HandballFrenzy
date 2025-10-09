using System.Collections.Generic;
using UnityEngine;

public class ArenaManager : MonoBehaviour
{
    public enum ArenaType { Wood = 0, Ice = 1, Sand = 2 }

    [Header("Prefabs de Cancha")]
    public GameObject woodPrefab;
    public GameObject icePrefab;
    public GameObject sandPrefab;

    [Header("Spawn")]
    public Transform spawnPoint;              // centro de la cancha
    public bool keepRotation = false;         // si tus canchas necesitan rotación específica

    [Header("Jugadores a ajustar")]
    public List<PlayerController> players = new List<PlayerController>(); // arrastra aquí tus PlayerController

    [Header("Arranque")]
    public ArenaType startArena = ArenaType.Wood;

    GameObject _currentArena;

    void Start()
    {
        LoadArena(startArena);
    }

    // Para conectar a botones sin parámetros
    public void LoadWood() => LoadArena(ArenaType.Wood);
    public void LoadIce()  => LoadArena(ArenaType.Ice);
    public void LoadSand() => LoadArena(ArenaType.Sand);

    // Para botones con int (0,1,2)
    public void LoadArenaByIndex(int i) => LoadArena((ArenaType)Mathf.Clamp(i, 0, 2));

    public void LoadArena(ArenaType type)
    {
        // Destruye la arena anterior
        if (_currentArena) { Destroy(_currentArena); _currentArena = null; }

        // Decide prefab
        GameObject prefab = null;
        switch (type)
        {
            case ArenaType.Wood: prefab = woodPrefab; break;
            case ArenaType.Ice:  prefab = icePrefab;  break;
            case ArenaType.Sand: prefab = sandPrefab; break;
        }
        if (!prefab) { Debug.LogWarning($"[ArenaManager] Prefab no asignado para {type}"); return; }

        // Instancia en spawn
        Vector3 pos = spawnPoint ? spawnPoint.position : Vector3.zero;
        Quaternion rot = keepRotation && spawnPoint ? spawnPoint.rotation : Quaternion.identity;
        _currentArena = Instantiate(prefab, pos, rot);

        // (Opcional) Reiniciar spawners/power-ups si dependen de la cancha:
        // foreach (var sp in _currentArena.GetComponentsInChildren<PowerUpSpawner>(true)) { sp.StartHalf(); }
    }
}
