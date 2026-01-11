using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Light))]
[DefaultExecutionOrder(-100)]
public class ShadowLightProcessor : MonoBehaviour
{
    [Header("环境设置")]
    public Transform wallTransform;

    [Header("物理参数")]
    public float shadowThickness = 1.5f;
    public float bias = 0.03f;
    [Range(0f, 5f)] public float rangeBuffer = 2.0f; // 稍微调大一点容错
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
        public bool isActive; // 本帧是否应该保持活跃
    }

    private Dictionary<Transform, ShadowInstance> _shadowInstanceMap = new Dictionary<Transform, ShadowInstance>();

    private Light _light;
    private int _shadowLayer;

    // 缓存池
    private List<Vector3> _tempVerts = new List<Vector3>();
    private List<int> _tempTris = new List<int>();

    void Start()
    {
        _light = GetComponent<Light>();
        _shadowLayer = LayerMask.NameToLayer("Shadow");
        if (_shadowLayer == -1) _shadowLayer = 0;

        if (debugMaterial == null)
        {
            debugMaterial = new Material(Shader.Find("Sprites/Default"));
            debugMaterial.color = new Color(0, 0, 0, 0.5f);
        }
    }

    void FixedUpdate()
    {
        if (wallTransform == null) return;

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
        for (int i = 0; i < ShadowCasterGroup.AllGroups.Count; i++)
        {
            var group = ShadowCasterGroup.AllGroups[i];
            if (group == null) continue;

            foreach (var item in group.Casters)
            {
                // --- 逻辑修改核心区域 Start ---

                // 条件A: 物体本身在灯光范围内
                bool inLightRange = IsInRange(item, lightPos, lightDir, lightRange, spotAngle);

                // 条件B: 影子目前正在被摄像机看见
                bool isVisibleOnScreen = false;
                ShadowInstance existingInstance;
                if (_shadowInstanceMap.TryGetValue(item.transform, out existingInstance))
                {
                    // Renderer.isVisible 只有在 Scene视图或Game视图 中被看见时才为 true
                    // 注意：这要求影子物体本身必须是 Active 的才能检测
                    if (existingInstance.go.activeSelf && existingInstance.mr.isVisible)
                    {
                        isVisibleOnScreen = true;
                    }
                }

                // 最终判定：只要满足其中一个条件，就必须更新！
                // 解释：如果物体在范围内，当然要更；如果物体跑出去了，但影子还在屏幕上拖得很长，也要更，防止穿帮。
                if (inLightRange || isVisibleOnScreen)
                {
                    UpdateShadowFor(item, lightPos);
                    hasAnyUpdate = true;
                }

                // --- 逻辑修改核心区域 End ---
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
                if (kvp.Value.go != null) Destroy(kvp.Value.go);
                continue;
            }

            // 只有当 isActive 为 false (意味着既不在灯光范围，也不在屏幕内) 时，才关掉
            if (!kvp.Value.isActive && kvp.Value.go.activeSelf)
            {
                kvp.Value.go.SetActive(false);
            }
        }

        if (keysToRemove != null)
        {
            foreach (var k in keysToRemove) _shadowInstanceMap.Remove(k);
        }

        // 4. 物理同步
        if (hasAnyUpdate)
        {
            Physics.SyncTransforms();
        }
    }

    bool IsInRange(ShadowCasterGroup.CasterItem item, Vector3 lightPos, Vector3 lightDir, float range, float angle)
    {
        Vector3 itemPos = item.transform.position;
        float distSqr = (itemPos - lightPos).sqrMagnitude;
        // 如果想要更激进的优化，这里可以用 range * range * 1.5f 之类的
        if (distSqr > range * range) return false;

        if (angle < 360f)
        {
            Vector3 dirToItem = (itemPos - lightPos).normalized;
            float angleToItem = Vector3.Angle(lightDir, dirToItem);
            if (angleToItem > (angle * 0.5f) + 10f) return false; // 角度稍微给大点宽容度
        }
        return true;
    }

    void UpdateShadowFor(ShadowCasterGroup.CasterItem item, Vector3 lightPos)
    {
        ShadowInstance instance;
        if (!_shadowInstanceMap.TryGetValue(item.transform, out instance))
        {
            instance = CreateShadowInstance(item);
            _shadowInstanceMap.Add(item.transform, instance);
        }

        // 标记为活跃，防止在第3步被关掉
        instance.isActive = true;

        if (!instance.go.activeSelf) instance.go.SetActive(true);

        // ... 下面的投影数学计算代码保持完全不变 ...
        // ... 直接使用你手里现有的 UpdateShadowFor 逻辑即可 ...
        // ... 重点是上面的 isActive 判断逻辑 ...

        // 为了方便你复制，我把数学部分简写在这里，请保留你之前完整的数学逻辑
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
        // 保持不变
        ShadowInstance instance = new ShadowInstance();
        GameObject go = new GameObject($"Shadow_For_{item.transform.name}");
        go.transform.SetParent(this.transform);
        go.layer = _shadowLayer;

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