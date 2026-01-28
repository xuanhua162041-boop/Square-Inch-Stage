using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    [Header("音量设置")]
    [Range(0f, 1f)]public float masterVolume = 1f;
    [Range(0f,1f)]public float sfxVolume = 1f;
    [Range(0f,1f)]public float bgmVolume = 1f;

    //内部变量
    private AudioSource _bgmSource;
    // 记录哪个loop clip 再哪个source上播放 以精准暂停
    private Dictionary<AudioClip, AudioSource> _activeLoops = new Dictionary<AudioClip, AudioSource>();
    private List<AudioSource> _sfxSources = new List<AudioSource>();

    protected override void OnAwake()
    {
        _bgmSource = CreateSource("Channel_BGM", true);
        for (int i = 0; i < 5; i++)
        {
            _sfxSources.Add(CreateSource($"Channel_SFX_{i}", false));
        }
    }
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource source = GetFreeSFXSource();
        source.PlayOneShot(clip,sfxVolume * masterVolume);
    }

    public void PlayLoop(AudioClip clip)
    {
        if (clip == null) return;

        if (_activeLoops.ContainsKey(clip))
        {
            //如果这个clip正在循环播放 则不再播放一次
            return;
        }

        AudioSource source = GetFreeSFXSource();//获取空闲的source

        source.clip = clip;
        source.loop = true;
        source.volume = sfxVolume * masterVolume;
        source.Play();

        _activeLoops.Add(clip, source);
    }
    public void StopLoop(AudioClip clip)
    {
        if (clip == null) return;
        if (_activeLoops.ContainsKey(clip))
        {
            AudioSource source = _activeLoops[clip];

            source.Stop();
            source.clip = null;
            source.loop = false;

            _activeLoops.Remove(clip);
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || _bgmSource.clip == clip) return;
        _bgmSource.clip = clip;
        _bgmSource.volume = bgmVolume * masterVolume;
        _bgmSource.Play();
    }

    private AudioSource CreateSource(string name, bool isLoop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(this.transform);
        AudioSource source = go.AddComponent<AudioSource>();
        source.loop = isLoop;
        source.playOnAwake = false;
        return source;

    }

    //AudioSource动态扩容对象池
    private AudioSource GetFreeSFXSource()
    {
        foreach(var source in _sfxSources)
        {
            if (!source.isPlaying && !_activeLoops.ContainsValue(source))
            {
                return source;
            }
        }
        //如果没有空闲的source 则创建一个新的
        var newSource  = CreateSource($"Channel_SFX_{_sfxSources.Count}", false);
        _sfxSources.Add(newSource);
        return newSource;
    }
}
