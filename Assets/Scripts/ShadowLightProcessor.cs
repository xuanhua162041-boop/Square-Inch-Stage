using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Light))]
[DefaultExecutionOrder(-100)]
[ExecuteAlways]
public class ShadowLightProcessor : MonoBehaviour
{
    [Header("Wall")]
    public Transform wallTransform;

    [Header("Shadow")]
    public float shadowThickness = 1.5f;
    public float bias = 0.03f;
    [Range(0f, 5f)] public float rangeBuffer = 2.0f;
    public PhysicMaterial shadowPhysicsMat;
    public bool cullOffscreenShadows = true;
    [Min(0f)] public float screenCullPadding = 0.5f;

    [Header("Debug")]
    public bool showDebugVisuals = false;
    public Material debugMaterial;

    private class ShadowInstance
    {
        public GameObject go;
        public Mesh mesh;
        public MeshFilter mf;
        public MeshCollider mc;
        public bool isActive;
        public ShadowFreezable freezableComp;
    }

    private readonly Dictionary<Transform, ShadowInstance> _shadowInstanceMap = new Dictionary<Transform, ShadowInstance>();
    private Light _light;
    private int _shadowLayer;
    private Transform _shadowContainer;

    private readonly List<Vector3> _tempVerts = new List<Vector3>();
    private readonly List<int> _tempTris = new List<int>();

    void Start() { InitData(); }
    void Update() { if (!Application.isPlaying) ProcessShadowLogic(); }
    void FixedUpdate() { if (Application.isPlaying) ProcessShadowLogic(); }

    void InitData()
    {
        _light = GetComponent<Light>();
        _shadowLayer = LayerMask.NameToLayer("Shadow");
        if (_shadowLayer == -1) _shadowLayer = 0;
        if (debugMaterial == null) debugMaterial = new Material(Shader.Find("Sprites/Default"));

        if (_shadowContainer == null)
        {
            var go = GameObject.Find("Dynamic_Shadow_Container");
            if (go == null)
            {
                go = new GameObject("Dynamic_Shadow_Container");
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
            }
            _shadowContainer = go.transform;
        }
    }

    void ProcessShadowLogic()
    {
        if (wallTransform == null || _light == null)
        {
            InitData();
            if (_light == null) return;
        }

        Vector3 lightPos = transform.position;
        float lightRange = _light.range + rangeBuffer;
        float spotAngle = _light.type == LightType.Spot ? _light.spotAngle : 360f;
        Vector3 lightDir = transform.forward;

        Plane[] cameraPlanes = null;
        if (Application.isPlaying && cullOffscreenShadows && Camera.main != null)
        {
            cameraPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        }

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

                    item.SyncGeometryIfNeeded();
                    if (item.srcVertices == null || item.srcTriangles == null || item.srcVertices.Length == 0 || item.srcTriangles.Length == 0) continue;

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

                    if (instance.freezableComp != null && instance.freezableComp.isFrozen)
                    {
                        continue;
                    }

                    bool inLightRange = IsInRange(item, lightPos, lightDir, lightRange, spotAngle);
                    bool isShadowVisible = cameraPlanes == null || IsShadowVisible(item, lightPos, cameraPlanes);
                    if (inLightRange && isShadowVisible)
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

        if (_shadowContainer != null) go.transform.SetParent(_shadowContainer);

        go.layer = _shadowLayer;
        go.hideFlags = HideFlags.DontSave;

        instance.go = go;
        instance.mf = go.AddComponent<MeshFilter>();

        instance.mc = go.AddComponent<MeshCollider>();
        instance.mc.convex = false;
        if (shadowPhysicsMat != null) instance.mc.material = shadowPhysicsMat;

        instance.mesh = new Mesh();
        instance.mesh.MarkDynamic();
        instance.mf.mesh = instance.mesh;

        instance.freezableComp = item.transform.GetComponent<ShadowFreezable>();

        return instance;
    }

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

        if (keysToRemove != null)
        {
            foreach (var k in keysToRemove) _shadowInstanceMap.Remove(k);
        }
    }

    bool IsInRange(ShadowCasterGroup.CasterItem item, Vector3 lightPos, Vector3 lightDir, float range, float angle)
    {
        Vector3 itemPos = item.transform.position;
        if ((itemPos - lightPos).sqrMagnitude > range * range) return false;
        if (angle < 360f && Vector3.Angle(lightDir, (itemPos - lightPos).normalized) > (angle * 0.5f) + 10f) return false;
        return true;
    }

    bool IsShadowVisible(ShadowCasterGroup.CasterItem item, Vector3 lightPos, Plane[] cameraPlanes)
    {
        if (wallTransform == null || item.srcVertices == null || item.srcVertices.Length == 0) return false;

        Vector3 planePoint = wallTransform.position;
        Vector3 planeNormal = wallTransform.up;
        Matrix4x4 localToWorld = item.transform.localToWorldMatrix;

        Vector3 firstWorldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[0]);
        Bounds shadowBounds = new Bounds(ProjectToWall(firstWorldVert, lightPos, planePoint, planeNormal), Vector3.zero);

        for (int i = 1; i < item.srcVertices.Length; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[i]);
            shadowBounds.Encapsulate(ProjectToWall(worldVert, lightPos, planePoint, planeNormal));
        }

        if (screenCullPadding > 0f)
        {
            shadowBounds.Expand(screenCullPadding);
        }

        return GeometryUtility.TestPlanesAABB(cameraPlanes, shadowBounds);
    }

    Vector3 ProjectToWall(Vector3 worldVert, Vector3 lightPos, Vector3 planePoint, Vector3 planeNormal)
    {
        Vector3 rayDir = (worldVert - lightPos).normalized;
        float denom = Vector3.Dot(planeNormal, rayDir);
        if (Mathf.Abs(denom) < 1e-5f) denom = 1e-5f;
        float t = Vector3.Dot(planeNormal, (planePoint - lightPos)) / denom;
        return lightPos + rayDir * t + planeNormal * bias;
    }

    void UpdateShadowFor(ShadowCasterGroup.CasterItem item, ShadowInstance instance, Vector3 lightPos)
    {
        Vector3 s = instance.go.transform.lossyScale;
        if (Mathf.Abs(s.x) < 1e-4f || Mathf.Abs(s.y) < 1e-4f || Mathf.Abs(s.z) < 1e-4f) return;

        Vector3 planePoint = wallTransform.position;
        Vector3 planeNormal = wallTransform.up;
        Matrix4x4 localToWorld = item.transform.localToWorldMatrix;
        Matrix4x4 worldToShadowLocal = instance.go.transform.worldToLocalMatrix;

        int count = item.srcVertices.Length;
        _tempVerts.Clear();
        if (_tempVerts.Capacity < count * 2) _tempVerts.Capacity = count * 2;

        for (int i = 0; i < count; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[i]);
            _tempVerts.Add(worldToShadowLocal.MultiplyPoint3x4(ProjectToWall(worldVert, lightPos, planePoint, planeNormal)));
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 worldVert = localToWorld.MultiplyPoint3x4(item.srcVertices[i]);
            Vector3 topPoint = ProjectToWall(worldVert, lightPos, planePoint, planeNormal);
            Vector3 bottomPoint = topPoint + planeNormal * shadowThickness;
            _tempVerts.Add(worldToShadowLocal.MultiplyPoint3x4(bottomPoint));
        }

        _tempTris.Clear();
        int srcTriCount = item.srcTriangles.Length;
        for (int i = 0; i < srcTriCount; i += 3)
        {
            _tempTris.Add(item.srcTriangles[i]);
            _tempTris.Add(item.srcTriangles[i + 1]);
            _tempTris.Add(item.srcTriangles[i + 2]);
        }
        for (int i = 0; i < srcTriCount; i += 3)
        {
            int off = count;
            _tempTris.Add(item.srcTriangles[i + 2] + off);
            _tempTris.Add(item.srcTriangles[i + 1] + off);
            _tempTris.Add(item.srcTriangles[i] + off);
        }
        for (int i = 0; i < srcTriCount; i += 3)
        {
            AddSideQuad(_tempTris, item.srcTriangles[i], item.srcTriangles[i + 1], count);
            AddSideQuad(_tempTris, item.srcTriangles[i + 1], item.srcTriangles[i + 2], count);
            AddSideQuad(_tempTris, item.srcTriangles[i + 2], item.srcTriangles[i], count);
        }

        instance.mesh.Clear();
        instance.mesh.SetVertices(_tempVerts);
        instance.mesh.SetTriangles(_tempTris, 0);
        instance.mesh.RecalculateBounds();

        instance.mc.sharedMesh = null;
        instance.mc.sharedMesh = instance.mesh;
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
