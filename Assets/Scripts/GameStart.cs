using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStart : MonoBehaviour
{
    public StartupScreen startupScreen;
    private void Start()
    {
        startupScreen.Init();
    }
}
