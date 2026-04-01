using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 挂在需要冻结阴影的物体上。
/// 冻结时：
///   1. 把物体移到 FrozenShadow 层
///   2. 在主光源位置生成一个复制灯，只照射 FrozenShadow 层，位置固定
///   3. 主光源排除 FrozenShadow 层
///   4. 物体动画/物理停止，碰撞体停止更新
/// 解冻时：恢复所有状态
/// </summary>
public class ShadowFreezable : MonoBehaviour
{
    [Header("定格参数")]
    public float freezeDuration = 5.0f;
    public bool isFrozen = false;

    [Header("主光源引用")]
    [Tooltip("场景中的主 Spot Light")]
    public Light mainLight;
    [Tooltip("专用的冻结投影灯，场景里预先配置好，默认隐藏")]
    public Light frozenLight;

    [Header("视音效果")]
    public GameObject FreezeSFXObj;
    public AudioClip DiDaSFX;
    public AudioClip UnfreezeSFX;

    [Header("状态回调")]
    public UnityEvent onFreezeStart;
    public UnityEvent onFreezeEnd;
    public UnityEvent<float> onFreezeTimer;

    private float _timer;
    private bool _unFreezeSFX = false;
    private Animator _animator;
    private Rigidbody _rigidbody;
    private Animator _freezeSFXAnim;

    // 冻结用的复制灯
    private Light _frozenLight;
    // 物体原始 layer（递归保存）
    private System.Collections.Generic.Dictionary<GameObject, int> _originalLayers
        = new System.Collections.Generic.Dictionary<GameObject, int>();
    // 主光源原始 culling mask
    private int _originalMainLightMask;

    private static readonly int FrozenShadowLayer = -1; // 运行时获取

    void Awake()
    {
        _animator  = GetComponentInChildren<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void ActivateFreeze()
    {
        _unFreezeSFX = false;
        _timer = freezeDuration;
        if (!isFrozen)
        {
            isFrozen = true;

            // 停止动画和物理
            if (_animator != null) _animator.enabled = false;
            if (_rigidbody != null)
            {
                _rigidbody.velocity        = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic     = true;
            }

            // 冻结阴影
            DoFreezeLight();

            StartCoroutine(TiDaClock());
            if (FreezeSFXObj != null)
            {
                var sfxObj = Instantiate(FreezeSFXObj, transform);
                _freezeSFXAnim = sfxObj.GetComponent<Animator>();
            }
            onFreezeStart?.Invoke();
        }
    }

    public void CancelFreeze()
    {
        if (isFrozen) PerformUnFreeze();
    }

    void DoFreezeLight()
    {
        if (mainLight == null) return;

        int frozenLayer = LayerMask.NameToLayer("FrozenShadow");
        if (frozenLayer == -1)
        {
            Debug.LogWarning("[ShadowFreezable] 请在 Tags & Layers 里添加 'FrozenShadow' 层");
            return;
        }

        // 1. 把物体（含子物体）移到 FrozenShadow 层
        _originalLayers.Clear();
        SetLayerRecursive(gameObject, frozenLayer, _originalLayers);

        // 2. 启用冻结灯，同步到主光源当前位置/旋转
        if (frozenLight != null)
        {
            frozenLight.transform.position = mainLight.transform.position;
            frozenLight.transform.rotation = mainLight.transform.rotation;
            frozenLight.gameObject.SetActive(true);
            _frozenLight = frozenLight;
        }
        // FrozenShadow 层（物体）+ WallReceiveShadow 层（墙面接收阴影）
        int wallLayer = LayerMask.NameToLayer("WallReceiveShadow");
        if (wallLayer == -1)
        {
            Debug.LogWarning("[ShadowFreezable] 请在 Tags & Layers 里添加 'WallReceiveShadow' 层");
            wallLayer = LayerMask.NameToLayer("Default");
        }
        _frozenLight.cullingMask = (1 << frozenLayer) | (1 << wallLayer);

        // 3. 主光源排除 FrozenShadow 层
        _originalMainLightMask   = mainLight.cullingMask;
        mainLight.cullingMask    = mainLight.cullingMask & ~(1 << frozenLayer);
    }

    void DoUnfreezeLight()
    {
        int frozenLayer = LayerMask.NameToLayer("FrozenShadow");

        // 恢复物体原始 layer
        foreach (var kvp in _originalLayers)
            if (kvp.Key != null) kvp.Key.layer = kvp.Value;
        _originalLayers.Clear();

        // 恢复主光源 culling mask
        if (mainLight != null)
            mainLight.cullingMask = _originalMainLightMask;

        // 隐藏冻结灯
        if (_frozenLight != null)
        {
            _frozenLight.gameObject.SetActive(false);
            _frozenLight = null;
        }
    }

    IEnumerator TiDaClock()
    {
        while (isFrozen)
        {
            yield return new WaitForSeconds(1f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(DiDaSFX);
        }
    }

    void Update()
    {
        if (!isFrozen) return;

        _timer -= Time.deltaTime;
        onFreezeTimer?.Invoke(Mathf.Clamp01(_timer / freezeDuration));

        if (_timer <= 1f && !_unFreezeSFX)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(UnfreezeSFX);
            _unFreezeSFX = true;
        }

        if (_timer <= 0f) PerformUnFreeze();
    }

    void PerformUnFreeze()
    {
        if (_freezeSFXAnim != null)
            _freezeSFXAnim.Play("TimeFreezeSFXDissolve");

        if (_animator  != null) _animator.enabled    = true;
        if (_rigidbody != null) _rigidbody.isKinematic = false;

        DoUnfreezeLight();

        isFrozen = false;
        _timer   = 0f;
        onFreezeEnd?.Invoke();
        onFreezeTimer?.Invoke(0f);
    }

    static void SetLayerRecursive(GameObject obj, int newLayer,
        System.Collections.Generic.Dictionary<GameObject, int> originalLayers)
    {
        originalLayers[obj] = obj.layer;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, newLayer, originalLayers);
    }
}
