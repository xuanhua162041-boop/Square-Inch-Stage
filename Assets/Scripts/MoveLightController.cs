using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveLightController : MonoBehaviour
{
    public float speed = 5;
    public Vector3 initialLocalPos;//位置初始记录，记录的是 局部坐标
    public Vector2 MaxOffestDis;//灯光的最大偏移量

    private void Start()
    {
        initialLocalPos = transform.localPosition;
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
        else if(Input.GetKey(KeyCode.J))
            h = -1;

        Vector3 moveDir = new Vector3(h,v,0) * Time.deltaTime*speed;
        Vector3 desiredLocalPos = transform.localPosition + moveDir;

        //限制x轴偏移量，限制的x变量，最小值：  初始值-限制距离   最大值： 初始值+限制距离
        float clampedX = Mathf.Clamp(desiredLocalPos.x, initialLocalPos.x - MaxOffestDis.x,
            initialLocalPos.x + MaxOffestDis.x);

        float clampedY = Mathf.Clamp(desiredLocalPos.y, initialLocalPos.y - MaxOffestDis.y,
            initialLocalPos.y + MaxOffestDis.y);

        transform.localPosition = new Vector3(clampedX, clampedY, initialLocalPos.z);

        //transform.position = Vector3.Lerp(transform.position, target, speed);
    }
}
