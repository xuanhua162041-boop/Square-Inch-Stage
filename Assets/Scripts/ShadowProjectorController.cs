using UnityEngine;

[ExecuteAlways]
public class ShadowProjectorController : MonoBehaviour
{
    [Header("=== 核心连接 ===")]
    [Tooltip("把负责拍影子的那个摄像机拖进来")]
    public Camera shadowCam;

    [Tooltip("拖入你之前创建的那个 RT_ShadowCookie，我只用它读取分辨率和格式，不会往里画东西")]
    public RenderTexture settingsTemplate;

    [Header("=== 防裁切设置 ===")]
    [Tooltip("额外增加摄像机的视距，防止远处物体被切掉")]
    public float extraFarClip = 100f;
    [Tooltip("手动指定最近裁剪面")]
    public float manualNearClip = 0.1f;

    // 内部变量
    private Light _mainLight;
    private RenderTexture _rtA;
    private RenderTexture _rtB;
    private bool _useBufferA = true;

    void OnEnable()
    {
        _mainLight = GetComponent<Light>();

        // 自动创建两个缓冲区（双缓冲核心）
        if (settingsTemplate != null)
        {
            _rtA = new RenderTexture(settingsTemplate);
            _rtA.name = "Internal_Shadow_A";
            _rtB = new RenderTexture(settingsTemplate);
            _rtB.name = "Internal_Shadow_B";
        }
        else
        {
            // 如果没拖模板，默认创建一个 1024 的
            _rtA = new RenderTexture(1024, 1024, 16, RenderTextureFormat.Default);
            _rtB = new RenderTexture(1024, 1024, 16, RenderTextureFormat.Default);
        }
    }

    void OnDisable()
    {
        // 清理垃圾
        if (_rtA != null) _rtA.Release();
        if (_rtB != null) _rtB.Release();

        // 还原现场
        if (_mainLight != null) _mainLight.cookie = null;
        if (shadowCam != null) shadowCam.targetTexture = null;
    }

    void LateUpdate()
    {
        if (_mainLight == null || shadowCam == null || _rtA == null) return;

        // --- 1. 同步参数 (防止裁切逻辑) ---
        shadowCam.transform.position = transform.position;
        shadowCam.transform.rotation = transform.rotation;

        shadowCam.fieldOfView = _mainLight.spotAngle;
        shadowCam.aspect = 1.0f;
        shadowCam.nearClipPlane = manualNearClip;
        // 这里的 Far 包含了你的额外距离，绝对不会切断远处物体
        shadowCam.farClipPlane = _mainLight.range + extraFarClip;

        // --- 2. 双缓冲渲染 (解决不刷新逻辑) ---

        // 这一帧该用谁？
        RenderTexture currentRT = _useBufferA ? _rtA : _rtB;
        _useBufferA = !_useBufferA; // 下一帧换另一个

        // A. 强制相机画到这张 RT 上
        shadowCam.targetTexture = currentRT;
        shadowCam.Render(); // 啪！拍一张

        // B. 把灯光的 Cookie 换成这张刚出炉的 RT
        // 因为 A 和 B 是两张不同的图，URP 此时会被迫更新缓存
        _mainLight.cookie = currentRT;
    }
}