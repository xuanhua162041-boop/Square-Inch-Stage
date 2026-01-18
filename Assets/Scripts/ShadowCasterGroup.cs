using UnityEngine;
using System.Collections.Generic;

// 1. 核心修改：加上这个，OnEnable 才能在编辑器模式下自动执行，从而注册进列表
[ExecuteAlways]
public class ShadowCasterGroup : MonoBehaviour
{
    // 全局静态注册表
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

    // 2. 修改：确保在编辑器下每次重新激活或脚本编译后，都能重新初始化数据并注册
    void OnEnable()
    {
        // 这是一个保险措施：如果 Casters 数据丢了（编辑器常见情况），重新获取
        if (Casters.Count == 0)
        {
            InitializeGroup();
        }

        if (!AllGroups.Contains(this))
        {
            AllGroups.Add(this);
        }
    }

    void OnDisable()
    {
        if (AllGroups.Contains(this))
        {
            AllGroups.Remove(this);
        }
    }

    // 3. 新增：右键菜单功能。
    // 在 Hierarchy 选中物体，在 Inspector 的脚本组件标题栏右键 -> 选择 "刷新 Mesh 数据"
    // 这样当你换了子物体的模型后，不需要重新运行游戏就能更新影子数据
    [ContextMenu("刷新 Mesh 数据")]
    public void RebuildData()
    {
        InitializeGroup();
    }

    private void InitializeGroup()
    {
        Casters.Clear();
        MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>(false); // false 表示不包括非活跃物体，看你需求

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            // 排除自己，只加子物体
            if (mf.transform == this.transform && mfs.Length > 1) continue;

            CasterItem item = new CasterItem();
            item.transform = mf.transform;
            // 注意：这里保存的是 local space 的顶点，运行时会通过 transform 变换
            item.srcVertices = mf.sharedMesh.vertices;
            item.srcTriangles = mf.sharedMesh.triangles;
            item.srcBounds = mf.sharedMesh.bounds;

            Casters.Add(item);
        }
    }
}