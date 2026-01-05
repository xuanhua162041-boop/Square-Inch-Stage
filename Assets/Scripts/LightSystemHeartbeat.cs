using UnityEngine;

[ExecuteAlways]
public class LightCookieRefresher : MonoBehaviour
{
    private Camera _cam;
    private Light _light;
    private Texture _cookieTexture; // 缓存 RT 引用

    [Header("=== 强力刷新设置 ===")]
    [Tooltip("每隔多少帧强制刷新一次 Cookie？(建议 3-5)")]
    public int refreshInterval = 3;

    [Tooltip("是否开启防止裁切模式")]
    public bool antiClipping = true;
    public float manualNearClip = 0.1f;
    public float extraFarClip = 100f;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        _light = GetComponentInParent<Light>();

        if (_light != null)
        {
            _cookieTexture = _light.cookie; // 记住你原本设置的 RT
        }
    }

    void LateUpdate()
    {
        if (_cam == null || _light == null) return;

        // --- 1. 视锥体同步 (防止裁切) ---
        if (antiClipping)
        {
            _cam.fieldOfView = _light.spotAngle;
            _cam.aspect = 1.0f;
            _cam.nearClipPlane = manualNearClip;
            // 只要灯光 Range 不变，Camera Far 就保持一个很大的值，稳如泰山
            _cam.farClipPlane = _light.range + extraFarClip;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        // --- 2. 【核弹】强制重置 Cookie ---
        // 只有在游戏运行时，或者编辑器里 Render Texture 确实在变的时候才执行

        // 使用 Time.frameCount 来降频，防止每帧刷新闪烁，同时节省性能
        if (Time.frameCount % refreshInterval == 0)
        {
            // 这一招叫“拔网线再插上”
            // 告诉 URP：我现在没有 Cookie 了
            _light.cookie = null;

            // 瞬间告诉 URP：哎嘿，我又有了
            // 这会强制渲染管线重新从 Render Texture 读取最新像素
            _light.cookie = _cookieTexture;
        }

        // --- 3. 编辑器模式下的保底 ---
        // 如果不在播放模式，为了让你拖动能看到效果
        if (!Application.isPlaying)
        {
            _light.cookie = _cookieTexture;
        }
    }
}