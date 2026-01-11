using UnityEngine;
using System.Collections.Generic;

public class ShadowCasterGroup : MonoBehaviour
{
    // --- 【新增】全局静态注册表 ---
    // 所有的 Group 都会存在这里，灯光脚本直接读这个列表，不需要 FindObjectsOfType
    public static readonly List<ShadowCasterGroup> AllGroups = new List<ShadowCasterGroup>();

    [System.Serializable]
    public class CasterItem
    {
        public Transform transform;
        public Vector3[] srcVertices;
        public int[] srcTriangles;
        public Bounds srcBounds;
    }

    public List<CasterItem> Casters { get; private set; } = new List<CasterItem>();

    void Awake()
    {
        InitializeGroup();
    }

    // --- 【新增】当脚本启用/生成时，自动加入列表 ---
    void OnEnable()
    {
        if (!AllGroups.Contains(this))
        {
            AllGroups.Add(this);
        }
    }

    // --- 【新增】当脚本禁用/销毁时，自动移出列表 ---
    void OnDisable()
    {
        if (AllGroups.Contains(this))
        {
            AllGroups.Remove(this);
        }
    }

    private void InitializeGroup()
    {
        Casters.Clear(); // 防止重复初始化
        MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>(false);

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            // 排除自己，只加子物体
            if (mf.transform == this.transform && mfs.Length > 1) continue;

            CasterItem item = new CasterItem();
            item.transform = mf.transform;
            item.srcVertices = mf.sharedMesh.vertices;
            item.srcTriangles = mf.sharedMesh.triangles;
            item.srcBounds = mf.sharedMesh.bounds;

            Casters.Add(item);
        }
    }
}