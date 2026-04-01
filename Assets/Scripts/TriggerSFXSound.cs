using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerSFXSound : MonoBehaviour
{
    /*public AudioClip hightLight;
    public AudioClip Pressed;
    public AudioClip Selected;*/
    private AudioSource currentSfxAudioSource;

    public void PlaySfxSound(AudioClip audioClip)
    {
        AudioManager.Instance.PlaySFX(audioClip);
    }
    public void playSfxSoundBack(AudioClip audioClip)
    {
        currentSfxAudioSource = AudioManager.Instance.PlaySFXBack(audioClip);
    }
    public void StopAndPlay(AudioClip audioClip)
    {
        currentSfxAudioSource.clip = null ;
        PlaySfxSound(audioClip);
    }
}
