using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class ShadowCasterMesh : MonoBehaviour
{
    [Header("核心设置")]
    public Transform wallTransform;
    public string lightTag = "ShadowLight";

    [Header("调试开关")]
    [Tooltip("勾选它：显示绿色的影子块（方便你调试位置）。\n取消勾选：影子块隐形（游戏正式运行时用，只留碰撞）。")]
    public bool showDebugVisuals = true;

    [Header("物理设置")]
    [Tooltip("影子厚度 (建议 1.5)")]
    public float shadowThickness = 1.5f;
    public float bias = 0.03f;
    public PhysicMaterial physicsMaterial;

    // --- 内部数据 ---
    private class ShadowInstance
    {
        public Transform lightTrans;
        public GameObject obj;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider mc;
        public Mesh mesh;
    }

    private List<ShadowInstance> _shadows = new List<ShadowInstance>();

    private Vector3[] _srcVertices;
    private int[] _srcTriangles;
    private int _srcVertCount;
    private int _srcTriCount;

    // 用于调试的材质 (代码自动生成一个绿色的)
    private Material _debugMaterial;

    void Start()
    {
        // 自动创建调试材质
        _debugMaterial = new Material(Shader.Find("Sprites/Default"));
        _debugMaterial.color = new Color(0, 1, 0, 0.5f); // 半透明绿色

        if (wallTransform == null)
        {
            var wall = GameObject.Find("Wall");
            if (wall != null) wallTransform = wall.transform;
            else { this.enabled = false; return; }
        }

        MeshFilter srcMf = GetComponent<MeshFilter>();
        if (srcMf == null) { this.enabled = false; return; }

        _srcVertices = srcMf.sharedMesh.vertices;
        _srcTriangles = srcMf.sharedMesh.triangles;
        _srcVertCount = _srcVertices.Length;
        _srcTriCount = _srcTriangles.Length;

        GameObject[] lights = GameObject.FindGameObjectsWithTag(lightTag);
        foreach (var l in lights) CreateShadowInstance(l.transform);
    }

    void CreateShadowInstance(Transform lightSource)
    {
        ShadowInstance instance = new ShadowInstance();
        instance.lightTrans = lightSource;

        GameObject go = new GameObject($"ShadowCollider_{lightSource.name}");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        int layer = LayerMask.NameToLayer("Shadow");
        if (layer != -1) go.layer = layer;

        instance.mf = go.AddComponent<MeshFilter>();
        instance.mr = go.AddComponent<MeshRenderer>();
        instance.mc = go.AddComponent<MeshCollider>();

        // 1. 物理：强制开启凸包 (配合 Blender 拆分)
        instance.mc.convex = true;
        instance.mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                     MeshColliderCookingOptions.EnableMeshCleaning |
                                     MeshColliderCookingOptions.WeldColocatedVertices;
        if (physicsMaterial != null) instance.mc.material = physicsMaterial;

        // 2. 视觉：根据开关决定是否显示
        instance.mr.material = _debugMaterial;
        instance.mr.enabled = showDebugVisuals; // <--- 关键：这里控制是否隐形
        instance.mr.shadowCastingMode = ShadowCastingMode.Off;
        instance.mr.receiveShadows = false;

        instance.mesh = new Mesh();
        instance.mesh.name = "ShadowHitbox";
        instance.mesh.MarkDynamic();

        instance.mf.mesh = instance.mesh;
        instance.obj = go;
        _shadows.Add(instance);
    }

    void FixedUpdate()
    {
        if (wallTransform == null) return;
        foreach (var instance in _shadows)
        {
            if (instance.lightTrans != null)
            {
                UpdateSingleShadow(instance);
                // 实时更新可见性
                if (instance.mr.enabled != showDebugVisuals)
                    instance.mr.enabled = showDebugVisuals;
            }
        }
    }

    void UpdateSingleShadow(ShadowInstance instance)
    {
        Vector3 lightPos = instance.lightTrans.position;
        Vector3 planePoint = wallTransform.position;
        Vector3 planeNormal = wallTransform.up;

        Vector3[] newVerts = new Vector3[_srcVertCount * 2];
        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Matrix4x4 worldToShadowLocal = instance.obj.transform.worldToLocalMatrix;

        for (int i = 0; i < _srcVertCount; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(_srcVertices[i]);
            Vector3 rayDir = (worldVert - lightPos).normalized;

            float denom = Vector3.Dot(planeNormal, rayDir);
            if (Mathf.Abs(denom) < 0.00001f) denom = 0.00001f;

            float t = Vector3.Dot(planeNormal, (planePoint - lightPos)) / denom;
            Vector3 hitPoint = lightPos + rayDir * t;
            hitPoint += planeNormal * bias;

            Vector3 localHitPoint = worldToShadowLocal.MultiplyPoint3x4(hitPoint);
            Vector3 localNormal = worldToShadowLocal.MultiplyVector(planeNormal).normalized;

            newVerts[i] = localHitPoint;
            newVerts[i + _srcVertCount] = localHitPoint + localNormal * shadowThickness;
        }

        // 依然构建三角形，因为 MeshCollider 需要它来计算形状
        List<int> tris = new List<int>();
        for (int i = 0; i < _srcTriCount; i += 3)
        {
            tris.Add(_srcTriangles[i]); tris.Add(_srcTriangles[i + 1]); tris.Add(_srcTriangles[i + 2]);
        }
        for (int i = 0; i < _srcTriCount; i += 3)
        {
            int off = _srcVertCount;
            tris.Add(_srcTriangles[i + 2] + off); tris.Add(_srcTriangles[i + 1] + off); tris.Add(_srcTriangles[i] + off);
        }
        for (int i = 0; i < _srcTriCount; i += 3)
        {
            AddSideQuad(tris, _srcTriangles[i], _srcTriangles[i + 1], _srcVertCount);
            AddSideQuad(tris, _srcTriangles[i + 1], _srcTriangles[i + 2], _srcVertCount);
            AddSideQuad(tris, _srcTriangles[i + 2], _srcTriangles[i], _srcVertCount);
        }

        instance.mesh.Clear();
        instance.mesh.vertices = newVerts;
        instance.mesh.SetTriangles(tris, 0);
        instance.mesh.RecalculateNormals();
        instance.mesh.RecalculateBounds();
        instance.mc.sharedMesh = instance.mesh;
    }

    void AddSideQuad(List<int> tris, int i1, int i2, int off)
    {
        tris.Add(i1); tris.Add(i2); tris.Add(i1 + off);
        tris.Add(i2); tris.Add(i2 + off); tris.Add(i1 + off);
    }
}