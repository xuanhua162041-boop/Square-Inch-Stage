using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Light))]
[DefaultExecutionOrder(-100)]
[ExecuteAlways] // 1. 核心修改：允许在编辑模式下运行
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

    private class ShadowInstance
    {
        public GameObject go;
        public Mesh mesh;
        public MeshFilter mf;
        public MeshCollider mc;
        public MeshRenderer mr;
        public bool isActive;
    }

    private Dictionary<Transform, ShadowInstance> _shadowInstanceMap = new Dictionary<Transform, ShadowInstance>();

    private Light _light;
    private int _shadowLayer;

    // 缓存池
    private List<Vector3> _tempVerts = new List<Vector3>();
    private List<int> _tempTris = new List<int>();

    void Start()
    {
        InitData();
    }

    // 2. 初始化逻辑提取，防止编辑模式下组件丢失
    void InitData()
    {
        _light = GetComponent<Light>();
        _shadowLayer = LayerMask.NameToLayer("Shadow");
        if (_shadowLayer == -1) _shadowLayer = 0;

        if (debugMaterial == null)
        {
            // 注意：编辑模式下频繁 new Material 可能会导致内存泄漏，但在简单调试下通常无碍
            // 建议将 debugMaterial 做成 Asset 拖进去
            debugMaterial = new Material(Shader.Find("Sprites/Default"));
            debugMaterial.color = new Color(0, 0, 0, 0.5f);
        }
    }

    // 3. 编辑模式下使用 Update 驱动，Play 模式下保持 FixedUpdate
    void Update()
    {
        if (!Application.isPlaying)
        {
            ProcessShadowLogic();
        }
    }

    void FixedUpdate()
    {
        if (Application.isPlaying)
        {
            ProcessShadowLogic();
        }
    }

    // 4. 将核心逻辑封装，供 Update 和 FixedUpdate 调用
    void ProcessShadowLogic()
    {
        if (wallTransform == null) return;
        if (_light == null) InitData(); // 编辑模式下的防御性检查

        Vector3 lightPos = transform.position;
        float lightRange = _light.range + rangeBuffer;
        float spotAngle = _light.type == LightType.Spot ? _light.spotAngle : 360f;
        Vector3 lightDir = transform.forward;
        bool hasAnyUpdate = false;

        // 1. 重置活跃标记
        foreach (var kvp in _shadowInstanceMap)
        {
            kvp.Value.isActive = false;
        }

        // 2. 遍历所有注册物体
        // 【注意】如果 ShadowCasterGroup 没有加 [ExecuteAlways]，这里在编辑模式下可能为空
        if (ShadowCasterGroup.AllGroups != null)
        {
            for (int i = 0; i < ShadowCasterGroup.AllGroups.Count; i++)
            {
                var group = ShadowCasterGroup.AllGroups[i];
                if (group == null) continue;

                foreach (var item in group.Casters)
                {
                    bool inLightRange = IsInRange(item, lightPos, lightDir, lightRange, spotAngle);

                    bool isVisibleOnScreen = false;
                    ShadowInstance existingInstance;

                    // 编辑模式下 Renderer.isVisible 的行为可能和 Game 视图不同，
                    // 这里为了编辑器调试方便，只要 activeSelf 为 true 就视为可见，或者保留原有逻辑
                    if (_shadowInstanceMap.TryGetValue(item.transform, out existingInstance))
                    {
                        if (existingInstance.go != null && existingInstance.go.activeSelf)
                        {
                            // 在编辑器模式下，Scene 视图也算 Camera，所以 isVisible 通常有效
                            if (existingInstance.mr.isVisible) isVisibleOnScreen = true;
                            // 编辑模式下强制更新，方便拖拽查看效果
                            if (!Application.isPlaying) isVisibleOnScreen = true;
                        }
                    }

                    if (inLightRange || isVisibleOnScreen)
                    {
                        UpdateShadowFor(item, lightPos);
                        hasAnyUpdate = true;
                    }
                }
            }
        }

        // 3. 处理需要隐藏的影子
        List<Transform> keysToRemove = null;

        foreach (var kvp in _shadowInstanceMap)
        {
            if (kvp.Key == null)
            {
                if (keysToRemove == null) keysToRemove = new List<Transform>();
                keysToRemove.Add(kvp.Key);
                SafeDestroy(kvp.Value.go); // 使用安全销毁
                continue;
            }

            if (!kvp.Value.isActive && kvp.Value.go != null && kvp.Value.go.activeSelf)
            {
                kvp.Value.go.SetActive(false);
            }
        }

        if (keysToRemove != null)
        {
            foreach (var k in keysToRemove) _shadowInstanceMap.Remove(k);
        }

        // 4. 物理同步 (编辑模式下不需要 SyncTransforms，否则可能导致编辑器卡顿)
        if (hasAnyUpdate && Application.isPlaying)
        {
            Physics.SyncTransforms();
        }
    }

    // 5. 编辑器清理逻辑：脚本禁用或销毁时，清理生成的临时影子，防止残留
    void OnDisable()
    {
        foreach (var kvp in _shadowInstanceMap)
        {
            if (kvp.Value.go != null) SafeDestroy(kvp.Value.go);
        }
        _shadowInstanceMap.Clear();
    }

    // 6. 辅助函数：根据模式选择销毁方式
    void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    bool IsInRange(ShadowCasterGroup.CasterItem item, Vector3 lightPos, Vector3 lightDir, float range, float angle)
    {
        if (item.transform == null) return false; // 防御性检查
        Vector3 itemPos = item.transform.position;
        float distSqr = (itemPos - lightPos).sqrMagnitude;
        if (distSqr > range * range) return false;

        if (angle < 360f)
        {
            Vector3 dirToItem = (itemPos - lightPos).normalized;
            float angleToItem = Vector3.Angle(lightDir, dirToItem);
            if (angleToItem > (angle * 0.5f) + 10f) return false;
        }
        return true;
    }

    void UpdateShadowFor(ShadowCasterGroup.CasterItem item, Vector3 lightPos)
    {
        ShadowInstance instance;
        // 如果 Map 里有，但 GameObject 已经被手动删了（编辑器常见情况），需要重建
        if (!_shadowInstanceMap.TryGetValue(item.transform, out instance) || instance.go == null)
        {
            if (_shadowInstanceMap.ContainsKey(item.transform)) _shadowInstanceMap.Remove(item.transform);

            instance = CreateShadowInstance(item);
            _shadowInstanceMap.Add(item.transform, instance);
        }

        instance.isActive = true;
        if (!instance.go.activeSelf) instance.go.SetActive(true);

        Vector3 planePoint = wallTransform.position;
        Vector3 planeNormal = wallTransform.up;
        Matrix4x4 localToWorld = item.transform.localToWorldMatrix;
        Matrix4x4 worldToShadowLocal = instance.go.transform.worldToLocalMatrix;

        int count = item.srcVertices.Length;
        _tempVerts.Clear();
        if (_tempVerts.Capacity < count * 2) _tempVerts.Capacity = count * 2;

        // 计算顶面
        for (int i = 0; i < count; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[i]);
            Vector3 rayDir = (worldVert - lightPos).normalized;
            float denom = Vector3.Dot(planeNormal, rayDir);
            if (Mathf.Abs(denom) < 0.00001f) denom = 0.00001f;
            float t = Vector3.Dot(planeNormal, (planePoint - lightPos)) / denom;
            Vector3 hitPoint = lightPos + rayDir * t;
            hitPoint += planeNormal * bias;

            _tempVerts.Add(worldToShadowLocal.MultiplyPoint3x4(hitPoint));
        }
        // 计算底面
        for (int i = 0; i < count; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[i]);
            Vector3 rayDir = (worldVert - lightPos).normalized;
            float denom = Vector3.Dot(planeNormal, rayDir);
            if (Mathf.Abs(denom) < 0.00001f) denom = 0.00001f;
            float t = Vector3.Dot(planeNormal, (planePoint - lightPos)) / denom;
            Vector3 hitPoint = lightPos + rayDir * t;
            hitPoint += planeNormal * bias;

            _tempVerts.Add(worldToShadowLocal.MultiplyPoint3x4(hitPoint + planeNormal * shadowThickness));
        }

        _tempTris.Clear();
        int srcTriCount = item.srcTriangles.Length;
        for (int i = 0; i < srcTriCount; i += 3)
        {
            _tempTris.Add(item.srcTriangles[i]); _tempTris.Add(item.srcTriangles[i + 1]); _tempTris.Add(item.srcTriangles[i + 2]);
        }
        for (int i = 0; i < srcTriCount; i += 3)
        {
            int off = count;
            _tempTris.Add(item.srcTriangles[i + 2] + off); _tempTris.Add(item.srcTriangles[i + 1] + off); _tempTris.Add(item.srcTriangles[i] + off);
        }
        for (int i = 0; i < srcTriCount; i += 3)
        {
            AddSideQuad(_tempTris, item.srcTriangles[i], item.srcTriangles[i + 1], count);
            AddSideQuad(_tempTris, item.srcTriangles[i + 1], item.srcTriangles[i + 2], count);
            AddSideQuad(_tempTris, item.srcTriangles[i + 2], item.srcTriangles[i], count);
        }

        instance.mesh.Clear();
        instance.mesh.vertices = _tempVerts.ToArray();
        instance.mesh.SetTriangles(_tempTris, 0);
        instance.mesh.RecalculateNormals();
        instance.mesh.RecalculateBounds();

        instance.mc.sharedMesh = null;
        instance.mc.sharedMesh = instance.mesh;

        if (instance.mr.enabled != showDebugVisuals) instance.mr.enabled = showDebugVisuals;
    }

    ShadowInstance CreateShadowInstance(ShadowCasterGroup.CasterItem item)
    {
        ShadowInstance instance = new ShadowInstance();
        GameObject go = new GameObject($"Shadow_For_{item.transform.name}");
        go.transform.SetParent(this.transform);
        go.layer = _shadowLayer;

        // 7. 核心修改：设置 HideFlags 防止生成的影子被保存到场景文件中
        // DontSave: 不保存到 Scene
        // NotEditable: 在 Inspector 中不可见（可选，为了调试你可以去掉这个）
        go.hideFlags = HideFlags.DontSave;

        instance.go = go;
        instance.mf = go.AddComponent<MeshFilter>();
        instance.mr = go.AddComponent<MeshRenderer>();
        instance.mc = go.AddComponent<MeshCollider>();

        instance.mr.material = debugMaterial;
        instance.mr.shadowCastingMode = ShadowCastingMode.Off;
        instance.mr.receiveShadows = false;

        instance.mc.convex = false;
        if (shadowPhysicsMat != null) instance.mc.material = shadowPhysicsMat;

        instance.mesh = new Mesh();
        instance.mesh.name = "DynamicShadowMesh";
        instance.mesh.MarkDynamic();
        instance.mf.mesh = instance.mesh;

        return instance;
    }

    void AddSideQuad(List<int> tris, int i1, int i2, int off)
    {
        tris.Add(i1); tris.Add(i2); tris.Add(i1 + off);
        tris.Add(i2); tris.Add(i2 + off); tris.Add(i1 + off);
    }
}