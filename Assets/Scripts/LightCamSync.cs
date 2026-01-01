using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class LightCamSync : MonoBehaviour
{
    private Camera _cam;
    private Light _light;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        _light = GetComponentInParent<Light>();
    }

    void LateUpdate()
    {
        if (_cam == null || _light == null) return;

        // --- 1. 死死咬住参数 ---
        _cam.fieldOfView = _light.spotAngle;
        _cam.aspect = 1.0f;
        _cam.nearClipPlane = _light.shadowNearPlane > 0.1f ? _light.shadowNearPlane : 0.1f;
        _cam.farClipPlane = _light.range;

        // --- 2. 死死咬住位置 ---
        transform.localPosition = Vector3.zero;

        // --- 3. 【防卡死核心】微抖动 ---
        // 既然你说“相机打开影子也不刷新”，说明 Unity 判定画面静止就偷懒了。
        // 我们每一帧都极其微小地旋转一下相机 (0.0001度)，
        // 逼迫 Unity 认为“相机动了！必须重画！”
        float jitter = Mathf.Sin(Time.time * 100f) * 0.0001f;
        transform.localRotation = Quaternion.Euler(0, 0, jitter);
    }
}
