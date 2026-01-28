using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ShadowFreezable : MonoBehaviour
{
    [Header("定格参数")]
    public float freezeDuration = 5.0f;
    public bool isFrozen = false;

    [Header("视音效果")]
    public float transitionDuration = 1f;
    [Tooltip("必须和Shader里的 Reference Name 完全一致！")]
    public string dissolveProperty = "_DissolveProgress";
    public AudioClip DiDaSFX;
    public AudioClip UnfreezeSFX;

    [Header("状态回调")]
    public UnityEvent onFreezeStart;
    public UnityEvent onFreezeEnd;
    public UnityEvent<float> onFreezeTimer;

    private float _timer;
    private GameObject _shadowGo;
    private MeshRenderer _shadowMR;
    private bool unFreezeSFX = false;

    public void RegisterShadow(GameObject go, MeshRenderer mr)
    {
        _shadowGo = go;
        _shadowMR = mr;
    }

    public void ActivateFreeze()
    {
        unFreezeSFX = false;
        _timer = freezeDuration;
        if (!isFrozen)
        {

            isFrozen = true;
            StartCoroutine(TiDaClock());
            onFreezeStart?.Invoke();
        }
    }

    public void CancelFreeze()
    {
        if (isFrozen) PerformUnFreeze();
    }

    IEnumerator TiDaClock()
    {
        Debug.Log("TiDaIenumerator");

        while (isFrozen)
        {
            Debug.Log("TiDaIenumerator");
            yield return new WaitForSeconds(1f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(DiDaSFX);
        }
    }

    private void Update()
    {
        if (isFrozen)
        {
            _timer -= Time.deltaTime;
            onFreezeTimer?.Invoke(Mathf.Clamp01(_timer / freezeDuration));

            if (_timer <= 1f && !unFreezeSFX)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(UnfreezeSFX);
                unFreezeSFX = true;
            }

            if (_timer <= 0f) PerformUnFreeze();
        }
    }

    void PerformUnFreeze()
    {
        if (_shadowGo != null && _shadowMR != null)
        {
            PlayCrossDissolveEffect();
        }
        else
        {
            Debug.LogWarning($"[{name}] 解冻失败：找不到影子引用。可能 ShadowLightProcessor 没运行？");
        }

        isFrozen = false;
        _timer = 0f;
        onFreezeEnd?.Invoke();
        onFreezeTimer?.Invoke(0f);
    }

    void PlayCrossDissolveEffect()
    {
        // === 1. 自检 ===
        if (_shadowMR.sharedMaterial == null) return;

        // === 2. 制造残影 ===
        GameObject ghost = Instantiate(_shadowGo, _shadowGo.transform.position, _shadowGo.transform.rotation, _shadowGo.transform.parent);
        ghost.name = _shadowGo.name + "_Ghost";

        Collider ghostCollider = ghost.GetComponent<Collider>();
        if (ghostCollider != null) Destroy(ghostCollider);

        MeshFilter ghostMf = ghost.GetComponent<MeshFilter>();
        Mesh originalMesh = _shadowGo.GetComponent<MeshFilter>().sharedMesh;
        if (originalMesh != null) ghostMf.mesh = Instantiate(originalMesh);

        // === 3. 材质准备 ===
        MeshRenderer ghostMr = ghost.GetComponent<MeshRenderer>();
        // 分离材质实例
        Material ghostMat = new Material(_shadowMR.sharedMaterial);
        Material realMat = new Material(_shadowMR.sharedMaterial);

        ghostMr.material = ghostMat;
        _shadowMR.material = realMat;

        // 防止干扰
        ghostMr.transform.DOKill();
        _shadowMR.transform.DOKill();

        // === 4. 设定数值 (关键！) ===
        float visibleVal = 1.1f;    // 完全显示

        // 【核心防穿帮】: 
        // 既然 0.5 会看见黑块，那新影子的起点必须是 -0.1 (Shader最小值)
        // 这样它才能“无中生有”，而不是“半路杀出”。
        float absoluteHideVal = -0.1f;

        // === 5. 执行并行双向动画 (Cross Dissolve) ===

        // --- 旧影子 (Ghost) ---
        // 状态：显示 -> 消散
        ghostMat.SetFloat(dissolveProperty, visibleVal);
        ghostMat.DOFloat(absoluteHideVal, dissolveProperty, transitionDuration)
            .SetEase(Ease.Linear);

        // --- 新影子 (Real) ---
        // 状态：彻底隐形 -> 显示
        // 先按死在 -0.1，防止瞬间穿帮
        realMat.SetFloat(dissolveProperty, absoluteHideVal);
        // 马上开始变到 1.1
        realMat.DOFloat(visibleVal, dissolveProperty, transitionDuration)
            .SetEase(Ease.Linear);

        // === 6. 清理 ===
        Destroy(ghost, transitionDuration + 0.1f);
        if (ghostMf.mesh != null) Destroy(ghostMf.mesh, transitionDuration + 0.1f);
    }
}