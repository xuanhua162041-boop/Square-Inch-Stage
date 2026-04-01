using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeFreezable : MonoBehaviour
{
    private Material material;
    private static readonly int EdgeWidthId = Shader.PropertyToID("_Float0");
    private static readonly int _color = Shader.PropertyToID("_Color0");
    Color Disscolor;
    Color Showcolor;
    private void Awake()
    {
        Disscolor = new Color(0, 0, 0, 0);
        Disscolor = new Color(10f, 10f, 10f, 1);
        //transform.SetParent(null);

        material = GetComponent<Renderer>().material;
        materialInit();
    }
    private void materialInit()
    {
        material.SetColor(_color, Disscolor);

        material.color = Color.black;
        material.SetFloat(EdgeWidthId, 0.58f);
        this.transform.localScale = Vector3.zero;
    }
    public void TimeFreeza()
    {

        material.SetColor(_color, Showcolor);
        materialInit();
        material.DOColor(Color.white,0.5f);
        material.DOFloat(10f, EdgeWidthId, 1f);
        this.transform.DOScale(new Vector3(150,150,150), 2f);
        //material.DOColor(Disscolor, 4f);
        //material.DOFloat()
        material.SetColor(_color, Disscolor);
        

    }
    
}
