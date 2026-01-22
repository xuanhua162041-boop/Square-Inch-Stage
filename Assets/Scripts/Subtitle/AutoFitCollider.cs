using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AutoFitCollider : MonoBehaviour
{
    private BoxCollider _boxCollider;
    private TMP_Text _text;

    private void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();
        _text = GetComponent<TMP_Text>();
    }
    
    public float UpdateColliderSize()
    {
        //1.强制TMP 重新计算网格，否色为旧的bounds
        _text.ForceMeshUpdate();
        //2.获取文字渲染后的局部边界
        Bounds bounds = _text.textBounds;
        //2.设置碰撞体的中心和大小 并适当的增加厚度 防止穿模
        _boxCollider.center = bounds.center;
        _boxCollider.size = new Vector3(bounds.size.x, bounds.size.y, 0.2f);
        return bounds.size.x;

    }
}
