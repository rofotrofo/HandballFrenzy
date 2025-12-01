using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpSpawner : MonoBehaviour
{
    [Header("Spawn Points (4 total: 2 por mitad)")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Prefabs de PowerUps")]
    public List<GameObject> powerUpPrefabs = new List<GameObject>();

    [Header("Timing por mitad")]
    [Tooltip("Duración de la mitad en segundos (1:30 = 90)")]
    public float halfDurationSeconds = 90f;
    [Tooltip("Intento de spawn cada X segundos")]
    public float spawnIntervalSeconds = 30f;
    [Tooltip("Mínimo a garantizar por mitad")]
    public int minSpawnsPerHalf = 2;

    [Header("Opcional")]
    public bool spawnImmediatelyOnHalfStart = true;
    public bool autoStartForTesting = true;

    private Coroutine halfRoutine;
    private int spawnedThisHalf = 0;

    private bool _isPaused = false;
    private float _elapsedTimeThisHalf = 0f;

    void Start()
    {
        GameStateManager.Source.OnGamePaused += OnGamePaused;
        GameStateManager.Source.OnGameUnpaused += OnGameUnpaused;

        if (autoStartForTesting)
            StartHalf();
    }

    private void OnDestroy()
    {
        GameStateManager.Source.OnGamePaused -= OnGamePaused;
        GameStateManager.Source.OnGameUnpaused -= OnGameUnpaused;
    }

    #region PAUSE LOGIC
    private void OnGamePaused()
    {
        _isPaused = true;
    }

    private void OnGameUnpaused()
    {
        _isPaused = false;
    }

    public bool IsSpawningActive()
    {
        return halfRoutine != null && !_isPaused;
    }

    public float GetHalfProgress()
    {
        return Mathf.Clamp01(_elapsedTimeThisHalf / halfDurationSeconds);
    }
    #endregion

    public void StartHalf()
    {
        StopHalf();
        spawnedThisHalf = 0;
        _elapsedTimeThisHalf = 0f;
        halfRoutine = StartCoroutine(HalfLoop());
    }

    public void StopHalf()
    {
        if (halfRoutine != null)
        {
            StopCoroutine(halfRoutine);
            halfRoutine = null;
        }
    }

    private IEnumerator HalfLoop()
    {
        float t = 0f;

        if (spawnImmediatelyOnHalfStart && !_isPaused)
            TrySpawnOnce();

        while (t < halfDurationSeconds)
        {
            float waitTime = spawnIntervalSeconds;
            while (waitTime > 0f)
            {
                if (!_isPaused)
                {
                    waitTime -= Time.deltaTime;
                    t += Time.deltaTime;
                    _elapsedTimeThisHalf = t;
                }
                yield return null;
            }

            if (!_isPaused)
                TrySpawnOnce();
        }

        if (!_isPaused)
        {
            int needed = Mathf.Max(0, minSpawnsPerHalf - spawnedThisHalf);
            for (int i = 0; i < needed; i++)
            {
                if (!TrySpawnOnce()) break;
            }
        }
    }

    private bool TrySpawnOnce()
    {
        if (powerUpPrefabs.Count == 0 || spawnPoints.Count == 0 || _isPaused)
            return false;

        List<Transform> free = new List<Transform>();
        foreach (var p in spawnPoints)
        {
            if (!p) continue;
            bool occupied = false;
            for (int i = 0; i < p.childCount; i++)
            {
                var child = p.GetChild(i);
                if (child.CompareTag("PowerUp")) { occupied = true; break; }
            }
            if (!occupied) free.Add(p);
        }

        if (free.Count == 0) return false;

        Transform point = free[Random.Range(0, free.Count)];
        GameObject prefab = powerUpPrefabs[Random.Range(0, powerUpPrefabs.Count)];
        if (!prefab) return false;

        var go = Instantiate(prefab, point.position, Quaternion.identity, point);
        if (!go.CompareTag("PowerUp")) go.tag = "PowerUp";

        spawnedThisHalf++;
        return true;
    }
}
