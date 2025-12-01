using System;
using UnityEngine;

public interface IAudioSource
{
    event Action<float> OnMasterVolumeChange;
    event Action<float> OnSFXVolumeChange;
    event Action<float> OnMusicVolumeChange;

    float CurrentMasterVolume { get; }
    float CurrentSFXVolume { get; }
    float CurrentMusicVolume { get; }
    void SetMasterVolume(float volume);
    void SetSFXVolume(float volume);
    void SetMusicVolume(float volume);

    void PlayLevelMusic(string audioName);
    void PlayOneShot(string audioName);
}
