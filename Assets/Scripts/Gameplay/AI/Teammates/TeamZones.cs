using UnityEngine;

[DisallowMultipleComponent]
public class TeamZones : MonoBehaviour
{
    public static TeamZones Instance { get; private set; }

    [Header("Zonas por estado (índice = slot 0..2)")]
    public Transform[] attackSpots = new Transform[3];
    public Transform[] defendSpots = new Transform[3];
    public Transform[] neutralSpots = new Transform[3];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TryGetSpot(Transform[] bank, int idx, out Vector2 pos)
    {
        pos = default;
        if (bank != null && idx >= 0 && idx < bank.Length && bank[idx] != null)
        {
            pos = bank[idx].position;
            return true;
        }
        return false;
    }
}