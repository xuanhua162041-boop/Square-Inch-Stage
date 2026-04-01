using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using static Unity.VisualScripting.Member;

public class StartupScreen : MonoBehaviour
{
    public VideoPlayer startCG;
    public VideoPlayer MainPlaneCG;

    public GameObject MainPlaneUI;
    public CanvasGroup MainPlaneUIAlpha;

    public AudioClip MainPlaneUIBGM;
    public AudioClip Level1BGM;

    public GameObject SettingPlane;
    public void Init()
    {

        startCG.gameObject.SetActive(false);
        MainPlaneCG.gameObject.SetActive(false);

        startCG.loopPointReached += EndWithVideoPlay;

        MainPlaneUIAlpha = MainPlaneUI.GetComponent<CanvasGroup>();
        MainPlaneUIAlpha.alpha = 0;
        MainPlaneUI.gameObject.SetActive(false);
        SettingPlane.SetActive(false);

        StartCG(startCG);
        LoadScene();

    }
    public void Skip()
    {
        startCG.gameObject.SetActive(false);
        Debug.Log("StartCG播放完毕!");
        StartCG(MainPlaneCG);
    }

    private void EndWithVideoPlay(VideoPlayer source)
    {
        source.gameObject.SetActive(false);
        Debug.Log("StartCG播放完毕!");
        StartCG(MainPlaneCG);
        AudioManager.Instance.PlayBGM(MainPlaneUIBGM);

    }
    public void StartCG(VideoPlayer video)
    {
        video.gameObject.SetActive(true);
        ShowMainSelectUI();
    }

    public void ShowMainSelectUI()
    {
        MainPlaneUIAlpha.alpha = 1;

        MainPlaneUI.SetActive(true);
    }

    private void LoadScene()
    {
        
        SceneLoader.Instance.LoadScene("Level1", false);
    }
    public void ActivateSceneManually()
    {
        SceneLoader.Instance.ActivateSceneManually();
    }

    public void ShowSettingUI()
    {
        SettingPlane.SetActive(true);
    }
    public void PlayLevelBGM()
    {
        AudioManager.Instance.PlayBGM(Level1BGM);
    }


    public void OnExitGame()
    {
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }



}
