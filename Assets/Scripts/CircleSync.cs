using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleSync : MonoBehaviour
{
    public static int PosID = Shader.PropertyToID("_PlayerPosition");
    public static int SizeID = Shader.PropertyToID("_Size");

    public Transform currentPosition;
    public Material WallMaterial;
    public Camera Camera;
    public LayerMask Mask;

    // 增加一个变量，用来记录当前洞的大小，用于渐变
    private float currentHoleSize = 0f;

    // 你希望洞最大有多大？（根据你的Shader，1.0通常是全开，你可以改大或者改小）
    public float maxHoleSize = 1.0f;
    private void Start()
    {
        Camera = Camera.main;   
    }
    void Update()
    {
        // -----------------------------------------------------------
        // 1. 位置逻辑：完全保留你原来的写法！(屏幕视口坐标)
        // -----------------------------------------------------------
        var view = Camera.WorldToViewportPoint(currentPosition.position);
        WallMaterial.SetVector(PosID, view);

        // -----------------------------------------------------------
        // 2. 大小逻辑：加入 Lerp 实现"慢慢跟随/边缘出发"
        // -----------------------------------------------------------
        var dir = Camera.transform.position - currentPosition.position;
        var ray = new Ray(currentPosition.position, dir.normalized);

        // 目标大小：如果被挡住就是 1 (或者你定义的最大值)，没挡住就是 0
        float targetSize = 0f;

        if (Physics.Raycast(ray, 3000, Mask))
        {
            targetSize = maxHoleSize; // 被挡住了，目标是张开洞
        }
        else
        {
            targetSize = 0f; // 没被挡住，目标是关闭洞
        }

        // 【关键修改】使用 Lerp 让数值平滑过渡
        currentHoleSize = Mathf.Lerp(currentHoleSize, targetSize, Time.deltaTime * 5f);

        // 传给 Shader
        WallMaterial.SetFloat(SizeID, currentHoleSize);
    }
}