using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ShadowPlatformObject : MonoBehaviour
{
    [Header("设置")]
    public string lightTag = "ShadowLight";//灯光的tag

    public LayerMask wallLayer = 7;//接受影子

    [Tooltip("生成的物理平台的厚度")]
    public float platformThickness = 2f;
    [Tooltip("生成的物理平台的指定方向")]
    public Vector3 fixedExtrudeDir = Vector3.back;

    [SerializeField]
    [Tooltip("Shadow的私有属性")]
    private class ShadowData
    {
        public Light lightObj; //哪个灯
        public Transform lightTrans;  //灯的transform引用
        public Mesh mesh;  //影子网格
        public MeshCollider col;  //影子的碰撞体
        public Vector3 lastLightPos;  //灯的上次的位置
        public Quaternion lastLightRot;  //灯上次的旋转
    }
    //管理所有影子的列表  因为多个灯会产生多个影子
    private List<ShadowData> shadowPool = new List<ShadowData>();

    //记录 自己上一次的位置
    private Vector3 lastSelfPos;
    private Quaternion lastSelfRot;




    private void Start()
    {
        //寻找所有的灯
        GameObject[] lights = GameObject.FindGameObjectsWithTag(lightTag);
        if (lights.Length == 0)
        {
            Debug.Log("未找到 标记为lightTag 的灯光 ");
            return;
        }

        //给每个灯生成一个子物体
        foreach (var l in lights)
        {
            CreateShadowChild(l.GetComponent<Light>());
        }
        //初始化位置记录
        lastSelfPos = transform.position;
        lastSelfRot = transform.rotation;

        //第一次进行强制计算(因为是懒加载)
        UpdateAllShadows();

    }

    void CreateShadowChild(Light lightSource)
    {
        if (lightSource == null) return;
        //创建子物体
        GameObject go = new GameObject($"Shadow_{lightSource.name}");
        go.layer = 6;
        go.transform.SetParent(this.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        //添加组件
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        MeshCollider mc = go.AddComponent<MeshCollider>();

        //设置属性
        mr.enabled = false;
        mc.convex = true;
        Mesh m = new Mesh { name = "ShadowMesh" };
        mf.mesh = m;

        //存入数据列表
        ShadowData data = new ShadowData();
        data.lightObj = lightSource;
        data.lightTrans = lightSource.transform;
        data.mesh = m;
        data.col = mc;
        data.lastLightPos = lightSource.transform.position;
        data.lastLightRot = lightSource.transform.rotation;
        shadowPool.Add(data);
    }

    private void Update()
    {
        //1.检查自己动没动
        bool selfMoved = (transform.position != lastSelfPos) || (transform.rotation != lastSelfRot);
        //2.检查灯动没动
        bool anyLightMoved = false;
        foreach (var data in shadowPool)
        {
            if (data.lightObj == null) continue;
            if (data.lightTrans.position != data.lastLightPos || data.lightTrans.rotation != data.lastLightRot)
            {
                anyLightMoved = true;
                break;
            }
        }

        if (selfMoved || anyLightMoved)
        {
            UpdateAllShadows();

            //更新记录
            lastSelfPos = transform.position;
            lastSelfRot = transform.rotation;
            foreach (var data in shadowPool)
            {
                if (data.lightObj == null) continue;
                data.lastLightPos = data.lightTrans.position;
                data.lastLightRot = data.lightTrans.rotation;
            }
        }
    }

    public void UpdateAllShadows()
    {

        MeshFilter casterMesh = GetComponent<MeshFilter>();
        if (casterMesh == null) return;
        //获取到障碍物的 所有顶点
        Vector3[] vertices = casterMesh.sharedMesh.vertices;
        //对每个影子进行计算
        foreach (var data in shadowPool)
        {
            if (data.lightObj == null) continue;
            CalculateSingleShadow(data, vertices);

        }
    }

    void CalculateSingleShadow(ShadowData data, Vector3[] vertices)
    {
        // 用来存储 墙上的影子 点
        List<Vector3> baseVerticesWorld = new List<Vector3>();

        RaycastHit hitInfo;
        var lightposition = data.lightTrans.position;

        // 获取灯光参数
        float lightRange = data.lightObj.range;
        float halfSpotAngle = data.lightObj.spotAngle * 0.5f;
        Vector3 lightForward = data.lightTrans.forward;
        bool isSpot = data.lightObj.type == LightType.Spot;

        // 容错缓冲区：让检测范围比实际灯光稍微大一点 (例如 10%)
        // 作用：防止物体在光照边缘移动时影子频繁闪烁或断裂
        float rangeBuffer = 1.1f;
        float angleBuffer = 5f; // 角度放宽 5 度

        //记录墙面的法线
        Vector3 wallNormal = Vector3.zero;
        bool hasNormal = false;

        // 1. 遍历障碍物的每一个顶点，收集影子点
        foreach (var vertex in vertices)
        {
            Vector3 worldVertex = transform.TransformPoint(vertex);
            Vector3 toVertexDir = worldVertex - lightposition;
            float disToVertex = toVertexDir.magnitude; // 光到物体的距离

            #region 影子平台生成条件校验 (带容错)

            // 第一道安检：距离 (乘上 1.1 倍容错)
            if (disToVertex > lightRange * rangeBuffer) continue;

            // 第二道安检：角度 (如果是聚光灯，加上 5 度容错)
            if (isSpot)
            {
                float angle = Vector3.Angle(lightForward, toVertexDir);
                if (angle > halfSpotAngle + angleBuffer) continue;
            }

            #endregion

            Vector3 dirFromLight = toVertexDir.normalized;
            // 计算剩余射程
            float remainingRange = (lightRange * rangeBuffer) - disToVertex;
            if (remainingRange <= 0) continue;

            // 射线起点往后一点点，防止打中物体自己
            Vector3 startPos = worldVertex + dirFromLight * 0.01f;

            // 2. 射线检测 (接力模式)
            if (Physics.Raycast(startPos, dirFromLight, out hitInfo, remainingRange, wallLayer))
            {
                baseVerticesWorld.Add(hitInfo.point);

                // 记录墙面法线，用于后续挤出方向
                if (!hasNormal)
                {
                    wallNormal = hitInfo.normal;
                    hasNormal = true;
                }
            }
        }

        // 3. 凸包处理：整理乱序点，防止大长方体生成的碰撞体缩成一团
        baseVerticesWorld = GetConvexHull(baseVerticesWorld);

        // 4. "晾衣线"修复逻辑：如果影子太细(共线)或者点太少，手动增肥变成板子
        // 这步解决了 "Coplanar" 报错，也解决了细线无法站人的问题
        if (baseVerticesWorld.Count < 3 || IsCollinear(baseVerticesWorld))
        {
            // 如果点实在太少(少于2个)，连线都成不了，直接不生成
            if (baseVerticesWorld.Count < 2)
            {
                data.mesh.Clear();
                return;
            }

            Vector3 startP = baseVerticesWorld[0];
            Vector3 endP = baseVerticesWorld[baseVerticesWorld.Count - 1];

            // 算出线的方向
            Vector3 lineDir = (endP - startP).normalized;
            // 防止重合点导致方向为0
            if (lineDir == Vector3.zero) lineDir = Vector3.right;

            // 设置 影子的"实体宽度" (向下延伸的距离)
            float thicknessAmount = 0.2f;
            Vector3 expandDir = Vector3.down; // 默认向下挤出 (你的思路)

            // 特殊情况处理：万一影子是竖着的一根柱子，往下挤没用，得往侧面挤
            // 判断方法：看线的方向是不是差不多垂直的 (Y轴分量很大)
            if (Mathf.Abs(lineDir.y) > 0.9f)
            {
                expandDir = Vector3.right; // 竖线就变粗
            }

            // 重建矩形：把 线段 + 向下延伸的线段 组合起来，形成一个面
            baseVerticesWorld.Clear();
            baseVerticesWorld.Add(startP);
            baseVerticesWorld.Add(endP);
            baseVerticesWorld.Add(endP + expandDir * thicknessAmount);
            baseVerticesWorld.Add(startP + expandDir * thicknessAmount);
        }

        // 保底逻辑：如果全过程没拿到法线(比如只触发了晾衣线逻辑)，才用默认方向；否则强制用墙面法线
        if (wallNormal == Vector3.zero) wallNormal = -fixedExtrudeDir;

        // --- 以下生成网格代码 ---

        int numBaseVertices = baseVerticesWorld.Count;
        var allVertices = new Vector3[numBaseVertices * 2];

        for (int i = 0; i < numBaseVertices; i++)
        {
            // 1. 墙上的点 (背面)
            allVertices[i] = transform.InverseTransformPoint(baseVerticesWorld[i]);

            // 2. 挤出的点 (正面) - 核心修复：使用墙面法线 wallNormal 进行挤出
            // 这样无论墙面旋转角度如何，影子都会垂直于墙面生长，保证有体积
            Vector3 extrudePos = baseVerticesWorld[i] + wallNormal * platformThickness;
            allVertices[i + numBaseVertices] = transform.InverseTransformPoint(extrudePos);
        }

        // 计算三角形连线
        int[] baseTriangles = TriangulateConvexPolygon(numBaseVertices, 0);
        int[] extrusTrangles = TriangulateConvexPolygon(numBaseVertices, numBaseVertices);
        System.Array.Reverse(extrusTrangles); // 反转正面法线

        var triangles = new List<int>();
        triangles.AddRange(baseTriangles);
        triangles.AddRange(extrusTrangles);

        // 手动缝合侧面
        for (int i = 0; i < numBaseVertices; i++)
        {
            int currentFront = i;
            int nextFront = (i + 1) % numBaseVertices;
            int currentBack = i + numBaseVertices;
            int nextBack = ((i + 1) % numBaseVertices) + numBaseVertices;

            triangles.Add(currentFront);
            triangles.Add(nextBack);
            triangles.Add(nextFront);

            triangles.Add(currentFront);
            triangles.Add(currentBack);
            triangles.Add(nextBack);
        }

        // 赋值与刷新
        data.mesh.Clear();
        data.mesh.vertices = allVertices;
        data.mesh.triangles = triangles.ToArray();

        data.mesh.RecalculateNormals();
        data.mesh.RecalculateBounds();

        // 重新赋值给 Collider (先置空以强制刷新)
        data.col.sharedMesh = null;
        data.col.sharedMesh = data.mesh;
    }

    // 辅助函数：判断一堆点是不是在一条直线上 (用于检测"晾衣线")
    bool IsCollinear(List<Vector3> points)
    {
        if (points.Count < 3) return true;
        Vector3 start = points[0];
        Vector3 end = points[points.Count - 1];
        Vector3 lineDir = (end - start).normalized;

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 currentDir = (points[i] - start).normalized;
            // 叉乘结果接近0说明共线
            if (Vector3.Cross(lineDir, currentDir).sqrMagnitude > 0.01f)
                return false;
        }
        return true;
    }


    /// <summary>
    /// 智能凸包算法：自动计算投影平面，解决 Plane 旋转导致影子变成一条线的问题
    /// </summary>
    List<Vector3> GetConvexHull(List<Vector3> points)
    {
        if (points.Count < 3) return points;

        // 1. 计算中心点
        Vector3 center = Vector3.zero;
        foreach (var p in points) center += p;
        center /= points.Count;

        // 2. 计算法线 (这一步是核心：自动判断影子躺在哪个平面上)
        Vector3 normal = Vector3.forward;
        bool normalFound = false;

        // 遍历点，利用叉乘找到垂直于影子的方向
        for (int i = 0; i < points.Count - 2; i++)
        {
            Vector3 v1 = (points[i] - center).normalized;
            Vector3 v2 = (points[i + 1] - center).normalized;
            Vector3 n = Vector3.Cross(v1, v2);
            // 只要向量长度足够，说明找到了该平面的朝向
            if (n.sqrMagnitude > 0.001f)
            {
                normal = n.normalized;
                normalFound = true;
                break;
            }
        }

        // 如果找不到法线，说明所有点真的共线(晾衣线)，直接返回交给 CalculateSingleShadow 里的增肥逻辑处理
        if (!normalFound) return points;

        // 3. 构建旋转：计算把"当前影子的面"旋转到"正对屏幕(XY平面)"需要的旋转量
        // 这样我们就可以忽略 Z 轴，舒服地进行 2D 排序了
        Quaternion rotateToFlat = Quaternion.FromToRotation(normal, Vector3.back);
        Quaternion rotateBack = Quaternion.Inverse(rotateToFlat);

        // 4. 投影并排序
        List<Vector3> sortedPoints = new List<Vector3>();
        foreach (var p in points)
        {
            // 关键修正：不再转到物体(Transform)局部坐标，而是根据墙面法线转平
            sortedPoints.Add(rotateToFlat * (p - center));
        }

        // 排序：现在 Z 轴已经是 0 了，放心比较 X 和 Y
        sortedPoints.Sort((a, b) =>
        {
            if (Mathf.Abs(a.x - b.x) < 0.001f) return a.y.CompareTo(b.y);
            return a.x.CompareTo(b.x);
        });

        // 5. 单调链算法 (标准的 2D 凸包计算)
        List<Vector3> hull = new List<Vector3>();
        // 辅助函数：计算 2D 叉乘
        float Cross(Vector3 a, Vector3 b, Vector3 o)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        // 下半链
        for (int i = 0; i < sortedPoints.Count; i++)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], sortedPoints[i]) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(sortedPoints[i]);
        }

        // 上半链
        int lowerCount = hull.Count;
        for (int i = sortedPoints.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], sortedPoints[i]) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(sortedPoints[i]);
        }

        if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);

        // 6. 还原回世界坐标
        List<Vector3> result = new List<Vector3>();
        foreach (var p in hull)
        {
            // 转回去 + 移回原位
            result.Add((rotateBack * p) + center);
        }

        return result;
    }


    /// <summary>
    /// 把一个多边形 切分成三角形
    /// </summary>
    /// <param name="vertexCount"></param>
    /// <param name="offext"></param>
    /// <returns></returns>
    private int[] TriangulateConvexPolygon(int vertexCount, int offext)
    {
        List<int> triangles = new List<int>();
        if (vertexCount < 3) return triangles.ToArray();

        //扇形切分发  固定第一个点 一次连接后面的点
        for (int i = 1; i < vertexCount - 1; i++)
        {
            triangles.Add(offext);//中心点
            triangles.Add(offext + i);//当前点
            triangles.Add(offext + i + 1);//下一个点

        }
        return triangles.ToArray();
    }
}