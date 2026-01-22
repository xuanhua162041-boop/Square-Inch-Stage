using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveLightController : MonoBehaviour
{
    public float speed = 5;

    // 改为记录世界坐标
    public Vector3 initialWorldPos;
    public Vector2 MaxOffestDis; // 灯光的最大偏移量（基于世界坐标轴的距离）

    private void Start()
    {
        // 1. 初始化时记录世界坐标，而不是局部坐标
        initialWorldPos = transform.position;
    }

    private void FixedUpdate()
    {
        float h = 0;
        float v = 0;
        if (Input.GetKey(KeyCode.I))
            v = 1;
        else if (Input.GetKey(KeyCode.K))
            v = -1;
        if (Input.GetKey(KeyCode.L))
            h = 1;
        else if (Input.GetKey(KeyCode.J))
            h = -1;

        // 2. 计算移动方向
        // 在世界坐标系下，Vector3(h, v, 0) 直接对应世界坐标的 X 和 Y 轴
        Vector3 moveDir = new Vector3(h, v, 0) * Time.deltaTime * speed;

        // 3. 计算期望的世界坐标位置
        // 使用 transform.position 而不是 localPosition
        Vector3 desiredWorldPos = transform.position + moveDir;

        // 4. 限制范围（基于世界坐标的初始点进行 Clamp）
        float clampedX = Mathf.Clamp(desiredWorldPos.x, initialWorldPos.x - MaxOffestDis.x,
            initialWorldPos.x + MaxOffestDis.x);

        float clampedY = Mathf.Clamp(desiredWorldPos.y, initialWorldPos.y - MaxOffestDis.y,
            initialWorldPos.y + MaxOffestDis.y);

        // 5. 赋值回 transform.position
        // 注意：Z轴保持初始的世界坐标Z值，防止灯光在深度上发生偏移
        transform.position = new Vector3(clampedX, clampedY, initialWorldPos.z);
    }
}