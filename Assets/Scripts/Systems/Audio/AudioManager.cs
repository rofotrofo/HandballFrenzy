using System;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : Singleton<IAudioSource>, IAudioSource
{
    private const float DEFAULT_SFX_VOLUME = 0.8f;
    private const float DEFAULT_MUSIC_VOLUME = 0.8f;
    private const float DEFAULT_MASTER_VOLUME = 0.6f;
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";

    [SerializeField] private AudioLibrary _audioLibrary;
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private AudioSource _sfxAudioSource;
    [SerializeField] private AudioSource _bgmAudioSource;
    [SerializeField] private AudioSource _ambientAudioSource;

    public float CurrentSFXVolume { get; private set; }
    public float CurrentMusicVolume { get; private set; }
    public float CurrentMasterVolume { get; private set; }

    public event Action<float> OnSFXVolumeChange;
    public event Action<float> OnMusicVolumeChange;
    public event Action<float> OnMasterVolumeChange;

    private void Start()
    {
        LoadAudioSettings();
    }

    private void LoadAudioSettings()
    {
        CurrentMasterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, DEFAULT_MASTER_VOLUME);
        CurrentSFXVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, DEFAULT_SFX_VOLUME);
        CurrentMusicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, DEFAULT_MUSIC_VOLUME);

        float masterDb = Mathf.Log10(CurrentMasterVolume == 0f ? 0.0001f : CurrentMasterVolume) * 20f;
        float sfxDb = Mathf.Log10(CurrentSFXVolume == 0f ? 0.0001f : CurrentSFXVolume) * 20f;
        float musicDb = Mathf.Log10(CurrentMusicVolume == 0f ? 0.0001f : CurrentMusicVolume) * 20f;

        _audioMixer.SetFloat("master_vol", masterDb);
        _audioMixer.SetFloat("sfx_vol", sfxDb);
        _audioMixer.SetFloat("bgm_vol", musicDb);
    }

    public void SetMasterVolume(float volume)
    {
        var volumeMixerValue = Mathf.Clamp01(volume);
        var dB = Mathf.Log10(volumeMixerValue == 0f ? 0.0001f : volumeMixerValue) * 20f;
        _audioMixer.SetFloat("master_vol", dB);

        CurrentMasterVolume = volume;

        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, volume);
        PlayerPrefs.Save();

        OnMasterVolumeChange?.Invoke(volume);
    }

    public void SetSFXVolume(float volume)
    {
        var volumeMixerValue = Mathf.Clamp01(volume);
        var dB = Mathf.Log10(volumeMixerValue == 0f ? 0.0001f : volumeMixerValue) * 20f;
        _audioMixer.SetFloat("sfx_vol", dB);

        CurrentSFXVolume = volume;

        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
        PlayerPrefs.Save();

        OnSFXVolumeChange?.Invoke(volume);
    }

    public void SetMusicVolume(float volume)
    {
        var volumeMixerValue = Mathf.Clamp01(volume);
        var dB = Mathf.Log10(volumeMixerValue == 0f ? 0.0001f : volumeMixerValue) * 20f;
        _audioMixer.SetFloat("bgm_vol", dB);

        CurrentMusicVolume = volume;

        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
        PlayerPrefs.Save();

        OnMusicVolumeChange?.Invoke(volume);
    }

    public void PlayLevelMusic(string audioName)
    {
        _bgmAudioSource.clip = (_audioLibrary.GetAudio(audioName));
        _bgmAudioSource.Play();
    }

    public void PlayOneShot(string audioName)
    {
        _sfxAudioSource.PlayOneShot(_audioLibrary.GetAudio(audioName));
    }

    public void PlayAmbientAudio(string audioName)
    {
        _ambientAudioSource.clip = _audioLibrary.GetAudio(audioName);
        _ambientAudioSource.Play();
    }

    public void StopAmbientAudio()
    {
        _ambientAudioSource.Stop();
    }
}