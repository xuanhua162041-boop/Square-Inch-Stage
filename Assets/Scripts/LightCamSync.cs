using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class LightCamSync : MonoBehaviour
{
    private Camera _cam;
    private Light _light;
   
    private void OnEnable()
    {
        _cam = GetComponent<Camera>();
        _light = GetComponentInParent<Light>();
        //this.transform.localPosition = Vector3.zero*0.999f;
    }
    private void Start()
    {
        //this.transform.localPosition = Vector3.zero * 1.001f;

    }

    private void LateUpdate()
    {
        if (_cam == null || _light == null) return;

        //1. 相机 同 灯光进行同步
        _cam.fieldOfView = _light.spotAngle;

        _cam.aspect = 1.0f;//宽高比1:1

        _cam.nearClipPlane = _light.shadowNearPlane > 0.1f ? _light.shadowNearPlane : 0.1f;
        _cam.farClipPlane = _light.range;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        /*//防止休眠 抖动
        if (!Application.isPlaying)
        {
            float micriJitter = Mathf.Sin(Time.time * 100f) * 0.0001f;
            transform.localRotation = Quaternion.Euler(0,0,micriJitter);
            transform.localPosition = Vector3.zero;
        }*/
        
    }

}
