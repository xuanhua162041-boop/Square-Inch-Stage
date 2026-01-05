using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)] // 保持在脚本执行顺序的最前面，确保物理计算前 Mesh 已更新
public class ShadowGroupCaster : MonoBehaviour
{
    [Header("核心设置")]
    public Transform wallTransform;
    public string lightTag = "ShadowLight";

    [Header("调试开关")]
    public bool showDebugVisuals = true;

    [Header("物理设置")]
    public float shadowThickness = 1.5f;
    public float bias = 0.03f;
    public PhysicMaterial physicsMaterial; // 务必在 Inspector 赋值一个高摩擦力的材质

    // --- 内部数据结构 ---
    private class SubMeshInfo
    {
        public Transform meshTrans;
        public Vector3[] srcVertices;
        public int[] srcTriangles;
        public int srcVertCount;
        public int srcTriCount;
    }

    private class ShadowInstance
    {
        public Transform lightTrans;
        public SubMeshInfo subMesh;
        public GameObject shadowObj;
        public Mesh mesh;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider mc;
    }

    private List<SubMeshInfo> _subMeshes = new List<SubMeshInfo>();
    private List<ShadowInstance> _shadows = new List<ShadowInstance>();
    private Material _debugMaterial;

    void Start()
    {
        _debugMaterial = new Material(Shader.Find("Sprites/Default"));
        _debugMaterial.color = new Color(0, 1, 0, 0.5f);

        if (wallTransform == null)
        {
            var wall = GameObject.Find("Wall");
            if (wall != null) wallTransform = wall.transform;
            else { this.enabled = false; return; }
        }

        // 1. 搜集 Mesh 信息
        MeshFilter[] childMfs = GetComponentsInChildren<MeshFilter>();
        foreach (var mf in childMfs)
        {
            if (mf.transform == this.transform) continue;

            SubMeshInfo info = new SubMeshInfo();
            info.meshTrans = mf.transform;
            info.srcVertices = mf.sharedMesh.vertices;
            info.srcTriangles = mf.sharedMesh.triangles;
            info.srcVertCount = info.srcVertices.Length;
            info.srcTriCount = info.srcTriangles.Length;
            _subMeshes.Add(info);
        }

        // 2. 创建影子实例
        GameObject[] lights = GameObject.FindGameObjectsWithTag(lightTag);
        foreach (var l in lights)
        {
            foreach (var sm in _subMeshes)
            {
                CreateShadowInstance(l.transform, sm);
            }
        }
    }

    void CreateShadowInstance(Transform lightSource, SubMeshInfo sm)
    {
        ShadowInstance instance = new ShadowInstance();
        instance.lightTrans = lightSource;
        instance.subMesh = sm;

        GameObject go = new GameObject($"Shadow_{lightSource.name}_{sm.meshTrans.name}");
        go.transform.SetParent(transform);

        int layer = LayerMask.NameToLayer("Shadow"); // 确保你有这个 Layer
        if (layer != -1) go.layer = layer;

        instance.mf = go.AddComponent<MeshFilter>();
        instance.mr = go.AddComponent<MeshRenderer>();
        instance.mc = go.AddComponent<MeshCollider>();

        // 【修改点 1】关闭 Convex。
        // 对于复杂的皮影轮廓，Convex 会填平凹陷，导致角色看似踩空或被挤出。
        // 只有当你的角色也使用 MeshCollider 时才必须开启 Convex，
        // 如果角色是 Capsule/Box Collider，这里设为 false 更精准。
        instance.mc.convex = false;

        if (physicsMaterial != null) instance.mc.material = physicsMaterial;

        instance.mr.material = _debugMaterial;
        instance.mr.enabled = showDebugVisuals;
        instance.mr.shadowCastingMode = ShadowCastingMode.Off;
        instance.mr.receiveShadows = false;

        instance.mesh = new Mesh();
        instance.mesh.name = "ShadowHitbox";
        instance.mesh.MarkDynamic(); // 标记为动态，优化频繁更新的性能

        instance.mf.mesh = instance.mesh;
        instance.shadowObj = go;
        _shadows.Add(instance);
    }

    void FixedUpdate()
    {
        if (wallTransform == null) return;

        bool hasUpdated = false;

        foreach (var instance in _shadows)
        {
            if (instance.lightTrans != null && instance.subMesh.meshTrans != null)
            {
                UpdateSingleShadow(instance);
                if (instance.mr.enabled != showDebugVisuals)
                    instance.mr.enabled = showDebugVisuals;

                hasUpdated = true;
            }
        }

        // 【修改点 2】强制物理同步
        // 在修改了 MeshCollider 的 sharedMesh 后，必须调用此方法
        // 否则物理引擎要等到下一帧才会刷新碰撞体，导致高速运动的角色穿过影子
        if (hasUpdated)
        {
            Physics.SyncTransforms();
        }
    }

    void UpdateSingleShadow(ShadowInstance instance)
    {
        Vector3 lightPos = instance.lightTrans.position;
        Vector3 planePoint = wallTransform.position;
        Vector3 planeNormal = wallTransform.up;

        var sm = instance.subMesh;
        Vector3[] newVerts = new Vector3[sm.srcVertCount * 2];

        Matrix4x4 localToWorld = sm.meshTrans.localToWorldMatrix;
        Matrix4x4 worldToShadowLocal = instance.shadowObj.transform.worldToLocalMatrix;

        for (int i = 0; i < sm.srcVertCount; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(sm.srcVertices[i]);
            Vector3 rayDir = (worldVert - lightPos).normalized;

            float denom = Vector3.Dot(planeNormal, rayDir);
            if (Mathf.Abs(denom) < 0.00001f) denom = 0.00001f;

            float t = Vector3.Dot(planeNormal, (planePoint - lightPos)) / denom;
            Vector3 hitPoint = lightPos + rayDir * t;
            hitPoint += planeNormal * bias;

            Vector3 localHitPoint = worldToShadowLocal.MultiplyPoint3x4(hitPoint);
            Vector3 localNormal = worldToShadowLocal.MultiplyVector(planeNormal).normalized;

            newVerts[i] = localHitPoint;
            newVerts[i + sm.srcVertCount] = localHitPoint + localNormal * shadowThickness;
        }

        // 构建三角形逻辑不变
        List<int> tris = new List<int>();
        for (int i = 0; i < sm.srcTriCount; i += 3)
        {
            tris.Add(sm.srcTriangles[i]); tris.Add(sm.srcTriangles[i + 1]); tris.Add(sm.srcTriangles[i + 2]);
        }
        for (int i = 0; i < sm.srcTriCount; i += 3)
        {
            int off = sm.srcVertCount;
            tris.Add(sm.srcTriangles[i + 2] + off); tris.Add(sm.srcTriangles[i + 1] + off); tris.Add(sm.srcTriangles[i] + off);
        }
        for (int i = 0; i < sm.srcTriCount; i += 3)
        {
            AddSideQuad(tris, sm.srcTriangles[i], sm.srcTriangles[i + 1], sm.srcVertCount);
            AddSideQuad(tris, sm.srcTriangles[i + 1], sm.srcTriangles[i + 2], sm.srcVertCount);
            AddSideQuad(tris, sm.srcTriangles[i + 2], sm.srcTriangles[i], sm.srcVertCount);
        }

        instance.mesh.Clear();
        instance.mesh.vertices = newVerts;
        instance.mesh.SetTriangles(tris, 0);
        instance.mesh.RecalculateNormals();
        instance.mesh.RecalculateBounds(); // 确保包围盒正确

        // 重新赋值 MeshCollider
        instance.mc.sharedMesh = null; // 先清空，确保 dirty flag 被触发
        instance.mc.sharedMesh = instance.mesh;
    }

    void AddSideQuad(List<int> tris, int i1, int i2, int off)
    {
        tris.Add(i1); tris.Add(i2); tris.Add(i1 + off);
        tris.Add(i2); tris.Add(i2 + off); tris.Add(i1 + off);
    }
}