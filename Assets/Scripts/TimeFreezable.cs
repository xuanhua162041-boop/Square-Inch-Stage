using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeFreezable : MonoBehaviour
{
    private Material material;
    private static readonly int EdgeWidthId = Shader.PropertyToID("_Float0");
    private void Awake()
    {
        //transform.SetParent(null);

        material = GetComponent<Renderer>().material;
        materialInit();
    }
    private void materialInit()
    {
        material.color = Color.black;
        material.SetFloat(EdgeWidthId, 0.58f);
        this.transform.localScale = Vector3.zero;
    }
    public void TimeFreeza()
    {
        materialInit();
        material.DOColor(Color.white,0.5f);
        material.DOFloat(10f, EdgeWidthId, 1f);
        this.transform.DOScale(new Vector3(108,108,108), 2f);
        material.DOColor(Color.black, 3f);


    }
}
