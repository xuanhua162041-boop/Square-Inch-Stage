using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.Video;

public class MemoryCanvasUI : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public List<AudioClip> audios;
    public Image image;
    [SerializeField]
    public List<VideoClip> Videos;
    [SerializeField]
    public List<Sprite> Images;

    private CanvasGroup canvasGroup;
    public static Action OnVideoEndAction;
    private void Awake()
    {
        videoPlayer.gameObject.SetActive(false);
        MemoryItem.CollectMemoryAction += ShowUI;
        videoPlayer.loopPointReached += OnVideoEnd;
        canvasGroup = this.gameObject.GetComponent<CanvasGroup>();

    }
    void ShowUI(MemoryType type)
    {
        Init(type);
        ShowMemory();
        videoPlayer.gameObject.SetActive(true);


    }

    void Init(MemoryType type)
    {
        switch (type) {
            case MemoryType.Cup:
                videoPlayer.clip = Videos[0];
                image.sprite = Images[0];
                AudioManager.Instance.PlaySFX(audios[0]);
                break;
            case MemoryType.Paper:
                videoPlayer.clip = Videos[1];
                image.sprite = Images[1];
                AudioManager.Instance.PlaySFX(audios[1]);

                break;
            case MemoryType.Light:
                videoPlayer.clip = Videos[2];
                image.sprite = Images[2];
                AudioManager.Instance.PlaySFX(audios[2]);

                break;
            case MemoryType.Fabric:
                videoPlayer.clip = Videos[3];
                image.sprite = Images[3];
                AudioManager.Instance.PlaySFX(audios[3]);

                break;

        }
    }

    void ShowMemory()
    {
        canvasGroup.DOFade(1, 1f);
    }
    void OnVideoEnd(VideoPlayer vp)
    {
        OnVideoEndAction?.Invoke();
        canvasGroup.DOFade(0, 0.6f).onComplete=()=> {
            videoPlayer.gameObject.SetActive(false);
            OnVideoEndAction?.Invoke();
        };
    }
}
