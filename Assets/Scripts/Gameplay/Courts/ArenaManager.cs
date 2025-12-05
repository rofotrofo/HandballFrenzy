using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ArenaManager : MonoBehaviour
{
    public enum ArenaType { Wood = 0, Ice = 1, Sand = 2 }

    [Header("Prefabs de Cancha")]
    public GameObject woodPrefab;
    public GameObject icePrefab;
    public GameObject sandPrefab;

    [Header("Spawn")]
    public Transform spawnPoint;
    public bool keepRotation = false;

    [Header("Jugadores a ajustar")]
    public List<PlayerController> players = new List<PlayerController>();

    [Header("Arranque")]
    public ArenaType startArena = ArenaType.Wood;
    private ArenaType _initialArena;

    GameObject _currentArena;

    public event Action<ArenaType> OnArenaChanged;

    public static ArenaManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _initialArena = startArena;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        LoadArena(startArena);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("ArenaManager: Escena recargada, arena actual: " + startArena);

        if (_currentArena == null)
        {
            LoadArena(startArena);
        }
    }

    public void ResetToInitialState()
    {
        startArena = _initialArena;
        if (_currentArena != null)
        {
            Destroy(_currentArena);
            _currentArena = null;
        }
        LoadArena(startArena);
        Debug.Log("ArenaManager reseteado a estado inicial: " + startArena);
    }

    public void ForceLoadArena(ArenaType type)
    {
        startArena = type;
        if (_currentArena != null)
        {
            Destroy(_currentArena);
            _currentArena = null;
        }
        LoadArena(type);
    }

    public void LoadWood() => LoadArena(ArenaType.Wood);
    public void LoadIce() => LoadArena(ArenaType.Ice);
    public void LoadSand() => LoadArena(ArenaType.Sand);

    public void LoadArenaByIndex(int i) => LoadArena((ArenaType)Mathf.Clamp(i, 0, 2));

    public void LoadArena(ArenaType type)
    {
        startArena = type;

        if (_currentArena)
        {
            Destroy(_currentArena);
            _currentArena = null;
        }

        GameObject prefab = null;
        switch (type)
        {
            case ArenaType.Wood: prefab = woodPrefab; break;
            case ArenaType.Ice: prefab = icePrefab; break;
            case ArenaType.Sand: prefab = sandPrefab; break;
        }

        if (!prefab)
        {
            Debug.LogWarning($"[ArenaManager] Prefab no asignado para {type}");
            return;
        }

        Vector3 pos = spawnPoint ? spawnPoint.position : Vector3.zero;
        Quaternion rot = keepRotation && spawnPoint ? spawnPoint.rotation : Quaternion.identity;
        _currentArena = Instantiate(prefab, pos, rot);

        Debug.Log($"Arena cargada: {type}");
        OnArenaChanged?.Invoke(type);
    }

    public ArenaType GetCurrentArenaType()
    {
        return startArena;
    }
}
