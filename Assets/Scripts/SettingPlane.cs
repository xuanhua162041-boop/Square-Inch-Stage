using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingPlane : MonoBehaviour
{
    public Slider musicSlider;
    public Slider SfxSlider;

    public Animator handLeft;
    public Animator handRight;

    public bool firstOpen = true;

    public GameObject rawImage;

    public AudioClip TestBGMClip;
    public AudioClip TestSFXClip;

    public float bgmTimer;
    private float _bgmTimer;
    public float sfxTimer;
    private float _sfxTimer;



    
    private void OnEnable()
    {
        rawImage.gameObject.SetActive(true);

        InitData();
    }
    private void Start()
    {
        musicSlider.value = AudioManager.Instance.bgmVolume;
        SfxSlider.value = AudioManager.Instance.sfxVolume;

        musicSlider.onValueChanged.AddListener(OnMusicValueChanged);
        SfxSlider.onValueChanged.AddListener(OnSfxValueChanged);
        firstOpen = false;

        _bgmTimer = bgmTimer;
        _sfxTimer = sfxTimer;
        InitData();


    }

    void OnMusicValueChanged(float value)
    {
        AudioManager.Instance.bgmVolume = value;
        handLeft.SetFloat("VolumeValue",value);
        PlayTestMusicSound();
        
    }

    void OnSfxValueChanged(float value)
    {
        AudioManager.Instance.sfxVolume = value;
        handRight.SetFloat("VolumeValue", value);
        PlayTestSFXSound();
        



    }

    public void SaveData()
    {
        PlayerPrefs.SetFloat("bgmVolume", musicSlider.value);
        PlayerPrefs.SetFloat("sfxValue", SfxSlider.value);
        Debug.Log("存储玩家设置 并返回到主界面中 ...");
        StartCoroutine(LetMewaitForSeconds());        
    }
    IEnumerator LetMewaitForSeconds()
    {
        yield return new WaitForSeconds(0.5f);
        rawImage.gameObject.SetActive(false);
        this.gameObject.SetActive(false);
    }
    void InitData()
    {
        if (firstOpen) return;

        if (PlayerPrefs.HasKey("bgmVolume"))
        {
            musicSlider.value = PlayerPrefs.GetFloat("bgmVolume");
            OnMusicValueChanged(musicSlider.value);

        }
        if (PlayerPrefs.HasKey("sfxValue"))
        {
            SfxSlider.value = PlayerPrefs.GetFloat("sfxValue");
            OnSfxValueChanged(SfxSlider.value);

        }
    }
    void PlayTestMusicSound()
    {
        if (_bgmTimer > 0) return;
        AudioManager.Instance.PlaySFX(TestBGMClip);
        _bgmTimer = bgmTimer;

    }
    void PlayTestSFXSound()
    {
        if (_sfxTimer > 0) return;
        AudioManager.Instance.PlaySFX(TestSFXClip);
        _sfxTimer = sfxTimer;

    }

    private void Update()
    {
        _sfxTimer-= Time.deltaTime;
        _bgmTimer-= Time.deltaTime;
    
    }
}
