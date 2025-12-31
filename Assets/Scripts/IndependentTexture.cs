using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.Port;

[ExecuteAlways]
public class IndependentTexture : MonoBehaviour
{
    [Header("拖入纹理")]
    public Texture2D mainTexture;
    [ColorUsage(true, true)]
    public Color tint = Color.white;
    public float size = 1.0f;
    [Range(0f, 1f)]
    public float smoothness = 0.5f;
    [Range(0f, 1f)]
    public float opacity = 1f;
    public float Alpha = 0f;





    private MaterialPropertyBlock _propertyBlock;
    private Renderer _renderer;

    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
    private static readonly int TintID = Shader.PropertyToID("_Tint");
    private static readonly int SizeID = Shader.PropertyToID("_Size");
    private static readonly int SmoothID = Shader.PropertyToID("_Smoothness");
    private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");


    private void OnEnable()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if(_renderer!=null && _renderer.sharedMaterial != null)
        {
            if(mainTexture==null) mainTexture =(Texture2D)_renderer.sharedMaterial.GetTexture(MainTexID);
        }
        UpdateTexture();
    }
    private void OnValidate()//脚本被加载 / 编辑器中检视面板的值被修改时 调用
    {
        UpdateTexture();
    }

    void UpdateTexture()
    {
        if(_renderer == null) _renderer = GetComponent<Renderer>();
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

        //获取渲染器当前的属性块
        _renderer.GetPropertyBlock(_propertyBlock);
        //修改材质属性
        if (mainTexture != null) _propertyBlock.SetTexture(MainTexID, mainTexture);
        _propertyBlock.SetColor(TintID, tint);
        _propertyBlock.SetFloat(SizeID, size);
        _propertyBlock.SetFloat(SmoothID, smoothness);
        _propertyBlock.SetFloat(OpacityID, opacity);
        _propertyBlock.SetFloat(AlphaID, Alpha);

        //应用修改后的属性块
        _renderer.SetPropertyBlock(_propertyBlock);
    }
}
