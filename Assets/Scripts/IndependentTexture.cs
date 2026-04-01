using UnityEngine;

[ExecuteAlways]
public class IndependentTexture : MonoBehaviour
{
    [Header("基础纹理与颜色")]
    public Texture2D mainTexture;
    [ColorUsage(true, true)] public Color tint = Color.white;

    [Header("自光照 (Emission)")]
    public Texture2D emissionMap;
    [ColorUsage(false, false)] public Color emissionColor = Color.black;
    [Range(0f, 10f)] public float emissionIntensity = 1.0f;

    [Header("其他 Shader 参数")]
    public float size = 1.0f;
    [Range(0f, 1f)] public float smoothness = 0.5f;
    [Range(0f, 1f)] public float opacity = 1f;
    public float alpha = 0f;

    private MaterialPropertyBlock _propertyBlock;
    private Renderer _renderer;

    // 常规属性 ID
    private static readonly int SizeID = Shader.PropertyToID("_Size");
    private static readonly int SmoothID = Shader.PropertyToID("_Smoothness");
    private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

    // 动态确定的 ID
    private int _mainTexPropID;
    private int _colorPropID;
    private int _emissionMapPropID;
    private int _emissionColorPropID;

    private void OnEnable()
    {
        Initialize();
        UpdateTexture();
    }

    private void OnValidate()
    {
        Initialize();
        UpdateTexture();
    }

    private void Initialize()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

        if (_renderer != null && _renderer.sharedMaterial != null)
        {
            Material mat = _renderer.sharedMaterial;

            // 1. 主纹理与基础色兼容
            if (mat.HasProperty("_BaseMap")) _mainTexPropID = Shader.PropertyToID("_BaseMap");
            else _mainTexPropID = Shader.PropertyToID("_MainTex");

            if (mat.HasProperty("_BaseColor")) _colorPropID = Shader.PropertyToID("_BaseColor");
            else if (mat.HasProperty("_Tint")) _colorPropID = Shader.PropertyToID("_Tint");
            else _colorPropID = Shader.PropertyToID("_Color");

            // 2. 自光照属性兼容 (URP 和 Built-in 通常都叫这两个名字，但需检查)
            _emissionMapPropID = Shader.PropertyToID("_EmissionMap");
            _emissionColorPropID = Shader.PropertyToID("_EmissionColor");

            // 初始化纹理（仅当面板为空时）
            if (mainTexture == null && mat.HasProperty(_mainTexPropID))
                mainTexture = (Texture2D)mat.GetTexture(_mainTexPropID);

            if (emissionMap == null && mat.HasProperty(_emissionMapPropID))
                emissionMap = (Texture2D)mat.GetTexture(_emissionMapPropID);
        }
    }

    public void UpdateTexture()
    {
        if (_renderer == null) return;
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(_propertyBlock);

        // --- 设置主纹理与颜色 ---
        if (mainTexture != null && _mainTexPropID != 0) _propertyBlock.SetTexture(_mainTexPropID, mainTexture);
        if (_colorPropID != 0) _propertyBlock.SetColor(_colorPropID, tint);

        // --- 设置自光照 (Emission) ---
        if (emissionMap != null) _propertyBlock.SetTexture(_emissionMapPropID, emissionMap);

        // 核心：强度是通过颜色相乘实现的 (HDR 逻辑)
        // 最终发射颜色 = 颜色值 * 2的强度次方 (或直接乘以强度，取决于Shader习惯)
        // Unity 标准做法是直接乘以强度
        Color finalEmission = emissionColor * emissionIntensity;
        _propertyBlock.SetColor(_emissionColorPropID, finalEmission);

        // 如果是 URP/Standard，可能还需要激活自光照关键字
        // 但使用 PropertyBlock 时，通常建议 Shader 默认是开启 Emission 的

        // --- 设置其他参数 ---
        _propertyBlock.SetFloat(SizeID, size);
        _propertyBlock.SetFloat(SmoothID, smoothness);
        _propertyBlock.SetFloat(OpacityID, opacity);
        _propertyBlock.SetFloat(AlphaID, alpha);

        _renderer.SetPropertyBlock(_propertyBlock);
    }
}