using UnityEngine;

public class LevelMusic : MonoBehaviour
{
    [SerializeField] private string _trackName;

    private void Start()
    {
        AudioManager.Source.PlayLevelMusic(_trackName);
    }
}
