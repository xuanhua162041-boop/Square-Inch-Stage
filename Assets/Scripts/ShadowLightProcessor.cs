using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Light))]
[DefaultExecutionOrder(-100)]
[ExecuteAlways]
public class ShadowLightProcessor : MonoBehaviour
{
    [Header("环境设置")]
    public Transform wallTransform;

    [Header("物理参数")]
    public float shadowThickness = 1.5f;
    public float bias = 0.03f;
    [Range(0f, 5f)] public float rangeBuffer = 2.0f;
    public PhysicMaterial shadowPhysicsMat;

    [Header("调试")]
    public bool showDebugVisuals = true;
    public Material debugMaterial;

    // 内部类
    private class ShadowInstance
    {
        public GameObject go;
        public Mesh mesh;
        public MeshFilter mf;
        public MeshCollider mc;
        public MeshRenderer mr;
        public bool isActive;
        public ShadowFreezable freezableComp; // 缓存组件
    }

    private Dictionary<Transform, ShadowInstance> _shadowInstanceMap = new Dictionary<Transform, ShadowInstance>();
    private Light _light;
    private int _shadowLayer;

    // === 1. 新增：专门装影子的容器（不随灯光移动） ===
    private Transform _shadowContainer;

    private List<Vector3> _tempVerts = new List<Vector3>();
    private List<int> _tempTris = new List<int>();

    void Start() { InitData(); }
    void Update() { if (!Application.isPlaying) ProcessShadowLogic(); }
    void FixedUpdate() { if (Application.isPlaying) ProcessShadowLogic(); }

    void InitData()
    {
        _light = GetComponent<Light>();
        _shadowLayer = LayerMask.NameToLayer("Shadow");
        if (_shadowLayer == -1) _shadowLayer = 0;
        if (debugMaterial == null) debugMaterial = new Material(Shader.Find("Sprites/Default"));

        // === 2. 初始化容器 ===
        // 找一下有没有现成的，没有就造一个
        if (_shadowContainer == null)
        {
            var go = GameObject.Find("Dynamic_Shadow_Container");
            if (go == null)
            {
                go = new GameObject("Dynamic_Shadow_Container");
                // 这一点很重要：容器不应该乱动
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
            }
            _shadowContainer = go.transform;

            // 它是独立的，不要设为 Light 的子物体！
            // 也不要设为 HideAndDontSave，方便你在 Hierarchy 里看
        }
    }

    void ProcessShadowLogic()
    {
        if (wallTransform == null || _light == null) { InitData(); if (_light == null) return; }

        Vector3 lightPos = transform.position;
        float lightRange = _light.range + rangeBuffer;
        float spotAngle = _light.type == LightType.Spot ? _light.spotAngle : 360f;
        Vector3 lightDir = transform.forward;

        foreach (var kvp in _shadowInstanceMap) kvp.Value.isActive = false;

        if (ShadowCasterGroup.AllGroups != null)
        {
            for (int i = 0; i < ShadowCasterGroup.AllGroups.Count; i++)
            {
                var group = ShadowCasterGroup.AllGroups[i];
                if (group == null) continue;

                foreach (var item in group.Casters)
                {
                    if (item.transform == null || !item.transform.gameObject.activeInHierarchy) continue;

                    if (!_shadowInstanceMap.TryGetValue(item.transform, out ShadowInstance instance))
                    {
                        instance = CreateShadowInstance(item);
                        _shadowInstanceMap.Add(item.transform, instance);
                    }
                    else if (instance.go == null)
                    {
                        _shadowInstanceMap.Remove(item.transform);
                        instance = CreateShadowInstance(item);
                        _shadowInstanceMap.Add(item.transform, instance);
                    }

                    instance.isActive = true;
                    if (!instance.go.activeSelf) instance.go.SetActive(true);

                    // === 3. 极简定格逻辑 ===
                    // 因为影子不是灯光的子物体，所以只要不更新Mesh，它就绝对静止
                    if (instance.freezableComp != null && instance.freezableComp.isFrozen)
                    {
                        continue; // 跳过计算 = 定格
                    }

                    // 正常更新
                    bool inLightRange = IsInRange(item, lightPos, lightDir, lightRange, spotAngle);
                    if (inLightRange || !Application.isPlaying)
                    {
                        UpdateShadowFor(item, instance, lightPos);
                    }
                }
            }
        }

        CleanupShadows();
        if (Application.isPlaying) Physics.SyncTransforms();
    }

    ShadowInstance CreateShadowInstance(ShadowCasterGroup.CasterItem item)
    {
        ShadowInstance instance = new ShadowInstance();
        GameObject go = new GameObject($"Shadow_For_{item.transform.name}");

        // === 4. 关键修改：挂到独立的容器下，而不是 this.transform ===
        if (_shadowContainer != null) go.transform.SetParent(_shadowContainer);

        go.layer = _shadowLayer;
        // 编辑器里不保存生成的影子，保持干净
        go.hideFlags = HideFlags.DontSave;

        instance.go = go;
        instance.mf = go.AddComponent<MeshFilter>();
        instance.mr = go.AddComponent<MeshRenderer>();
        instance.mr.material = debugMaterial;
        instance.mr.shadowCastingMode = ShadowCastingMode.Off;
        instance.mr.receiveShadows = false;

        instance.mc = go.AddComponent<MeshCollider>();
        instance.mc.convex = false;
        if (shadowPhysicsMat != null) instance.mc.material = shadowPhysicsMat;

        instance.mesh = new Mesh();
        instance.mesh.MarkDynamic();
        instance.mf.mesh = instance.mesh;

        instance.freezableComp = item.transform.GetComponent<ShadowFreezable>();
        if (instance.freezableComp!=null)
        {
            //能被定格 就交给这个物体自己的 影子管理脚本
            instance.freezableComp.RegisterShadow(instance.go, instance.mr);
        }

        return instance;
    }

    // --- 以下数学部分保持原样，无需改动 ---

    void CleanupShadows()
    {
        List<Transform> keysToRemove = null;
        foreach (var kvp in _shadowInstanceMap)
        {
            if (kvp.Key == null)
            {
                if (keysToRemove == null) keysToRemove = new List<Transform>();
                keysToRemove.Add(kvp.Key);
                SafeDestroyShadow(kvp.Value);
                continue;
            }
            if (!kvp.Value.isActive && kvp.Value.go.activeSelf)
            {
                kvp.Value.go.SetActive(false);
            }
        }
        if (keysToRemove != null) foreach (var k in keysToRemove) _shadowInstanceMap.Remove(k);
    }

    bool IsInRange(ShadowCasterGroup.CasterItem item, Vector3 lightPos, Vector3 lightDir, float range, float angle)
    {
        Vector3 itemPos = item.transform.position;
        if ((itemPos - lightPos).sqrMagnitude > range * range) return false;
        if (angle < 360f && Vector3.Angle(lightDir, (itemPos - lightPos).normalized) > (angle * 0.5f) + 10f) return false;
        return true;
    }

    void UpdateShadowFor(ShadowCasterGroup.CasterItem item, ShadowInstance instance, Vector3 lightPos)
    {
        Vector3 planePoint = wallTransform.position;
        Vector3 planeNormal = wallTransform.up;
        Matrix4x4 localToWorld = item.transform.localToWorldMatrix;
        Matrix4x4 worldToShadowLocal = instance.go.transform.worldToLocalMatrix;

        int count = item.srcVertices.Length;
        _tempVerts.Clear();
        if (_tempVerts.Capacity < count * 2) _tempVerts.Capacity = count * 2;

        // 顶面
        for (int i = 0; i < count; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[i]);
            Vector3 rayDir = (worldVert - lightPos).normalized;
            float denom = Vector3.Dot(planeNormal, rayDir);
            if (Mathf.Abs(denom) < 1e-5f) denom = 1e-5f;
            float t = Vector3.Dot(planeNormal, (planePoint - lightPos)) / denom;
            _tempVerts.Add(worldToShadowLocal.MultiplyPoint3x4(lightPos + rayDir * t + planeNormal * bias));
        }
        // 底面
        for (int i = 0; i < count; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[i]);
            Vector3 rayDir = (worldVert - lightPos).normalized;
            float denom = Vector3.Dot(planeNormal, rayDir);
            if (Mathf.Abs(denom) < 1e-5f) denom = 1e-5f;
            float t = Vector3.Dot(planeNormal, (planePoint - lightPos)) / denom;
            Vector3 bottomPoint = lightPos + rayDir * t + planeNormal * (bias + shadowThickness);
            _tempVerts.Add(worldToShadowLocal.MultiplyPoint3x4(bottomPoint));
        }

        _tempTris.Clear();
        int srcTriCount = item.srcTriangles.Length;
        for (int i = 0; i < srcTriCount; i += 3) { _tempTris.Add(item.srcTriangles[i]); _tempTris.Add(item.srcTriangles[i + 1]); _tempTris.Add(item.srcTriangles[i + 2]); }
        for (int i = 0; i < srcTriCount; i += 3) { int off = count; _tempTris.Add(item.srcTriangles[i + 2] + off); _tempTris.Add(item.srcTriangles[i + 1] + off); _tempTris.Add(item.srcTriangles[i] + off); }
        for (int i = 0; i < srcTriCount; i += 3) { AddSideQuad(_tempTris, item.srcTriangles[i], item.srcTriangles[i + 1], count); AddSideQuad(_tempTris, item.srcTriangles[i + 1], item.srcTriangles[i + 2], count); AddSideQuad(_tempTris, item.srcTriangles[i + 2], item.srcTriangles[i], count); }

        instance.mesh.Clear();
        instance.mesh.SetVertices(_tempVerts);
        instance.mesh.SetTriangles(_tempTris, 0);
        instance.mesh.RecalculateNormals();
        instance.mesh.RecalculateBounds();

        instance.mc.sharedMesh = null;
        instance.mc.sharedMesh = instance.mesh;

        if (instance.mr.enabled != showDebugVisuals) instance.mr.enabled = showDebugVisuals;
    }

    void AddSideQuad(List<int> tris, int i1, int i2, int off)
    {
        tris.Add(i1); tris.Add(i2); tris.Add(i1 + off);
        tris.Add(i2); tris.Add(i2 + off); tris.Add(i1 + off);
    }

    void SafeDestroyShadow(ShadowInstance instance)
    {
        if (instance == null) return;
        if (instance.mesh != null) { if (Application.isPlaying) Destroy(instance.mesh); else DestroyImmediate(instance.mesh); }
        if (instance.go != null) { if (Application.isPlaying) Destroy(instance.go); else DestroyImmediate(instance.go); }
    }
}