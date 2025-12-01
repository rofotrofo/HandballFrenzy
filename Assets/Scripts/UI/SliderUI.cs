using UnityEngine;
using UnityEngine.UI;

public class SliderUI : MonoBehaviour
{
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _musicSlider;

    private void Start()
    {
        LoadVolumeValues();
        _masterSlider.onValueChanged.AddListener(AudioManager.Source.SetMasterVolume);
        _sfxSlider.onValueChanged.AddListener(AudioManager.Source.SetSFXVolume);
        _musicSlider.onValueChanged.AddListener(AudioManager.Source.SetMusicVolume);
    }

    private void LoadVolumeValues()
    {
        _masterSlider.value = AudioManager.Source.CurrentMasterVolume;
        _sfxSlider.value = AudioManager.Source.CurrentSFXVolume;
        _musicSlider.value = AudioManager.Source.CurrentMusicVolume;
    }
}
