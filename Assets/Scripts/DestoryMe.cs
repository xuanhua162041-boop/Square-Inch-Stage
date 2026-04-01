using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestoryMe : MonoBehaviour
{
    public void Destory(float time)
    {
        Destroy(this, time);
    }
}
