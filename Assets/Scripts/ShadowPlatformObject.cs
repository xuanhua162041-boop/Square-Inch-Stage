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
        this.gameObject.layer = 8;
        //寻找所有的灯
        GameObject[] lights = GameObject.FindGameObjectsWithTag(lightTag);
        if (lights.Length == 0)
        {
            Debug.Log("未找到 标记为lightTag 的灯光 ");
            return;
        }

        //给每个灯生成一个子物体
        foreach (var l in lights) { 
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
        go.transform.SetParent(this.transform,false);
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
        bool selfMoved = (transform.position!=lastSelfPos)|| (transform.rotation!=lastSelfRot);
        //2.检查灯动没动
        bool anyLightMoved =false;
        foreach(var data in shadowPool)
        {
            if (data.lightObj == null) continue;
            if (data.lightTrans.position!= data.lastLightPos || data.lightTrans.rotation!=data.lastLightRot)
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
            foreach(var data in shadowPool)
            {
                if(data.lightObj == null) continue;
                data.lastLightPos=data.lightTrans.position;
                data.lastLightRot=data.lightTrans.rotation;
            }
        }
    }

    public void UpdateAllShadows()
    {

        MeshFilter casterMesh = GetComponent<MeshFilter>();
        if (casterMesh == null) return;
        //获取到障碍物的 所有顶点
        Vector3[]vertices = casterMesh.sharedMesh.vertices;
        //对每个影子进行计算
        foreach(var data in shadowPool)
        {
            if (data.lightObj == null) continue;
            CalculateSingleShadow(data, vertices);

        }



        
    }

    void CalculateSingleShadow(ShadowData data , Vector3[] vertices)
    {
        //用来存储 墙上的影子 点
        List<Vector3> baseVerticesWorld = new List<Vector3>();

        //存储 '墙面朝向'(法线)  用于挤出厚度（现改为指定方向
        //Vector3 extrusionDirection = Vector3.zero;

        //存储 射线打中墙的信息
        RaycastHit hitInfo;
        var lightposition = data.lightTrans.position;//记录灯的位置

        //遍历障碍物的每一个顶点
        foreach (var vertex in vertices)
        {
            //1.[坐标转换] 把顶点的'局部坐标' 转换为'世界坐标'
            //因为障碍物可能移动了  所以需要世界中的真实位置
            Vector3 worldVertex = transform.TransformPoint(vertex);
            //2.计算出 光源->障碍物顶点的 方向
            Vector3 dirFromLight = (worldVertex - lightposition).normalized;
            //3.从光源出发 沿方向射出,直到检测到wallLayer层
            if (Physics.Raycast(lightposition, dirFromLight, out hitInfo, 100f, wallLayer))
            {
                //射到 墙 就把   在[墙上的点] 存进列表
                baseVerticesWorld.Add(hitInfo.point);
                //记一下 墙是朝向哪边的(法线)  决定 网格朝哪个方向 增加厚度
                /*if (extrusionDirection == Vector3.zero)
                {
                    extrusionDirection = hitInfo.normal;
                }*/
            }
        }//以上 是存储了 影子在墙上的 顶点

        //顶点不够成为一个面 则清空并退出
        if (baseVerticesWorld.Count < 2)//原本是3 ， 这里是为了出现 一条线的影子
        {
            data.mesh.Clear();
            return;
        }

        //如果 只有2 个点  或者点都在一条线上 
        //此时用凸包会报错， 所以 加一个偏移点成为三角形， 挤出后就是三棱柱
        if(baseVerticesWorld.Count == 2)
        {
            Vector3 fakePoint = baseVerticesWorld[0] + Vector3.up * 0.01f;
            baseVerticesWorld.Add(fakePoint);
        }


        int numBaseVertices = baseVerticesWorld.Count;
        //创建一个新的顶点数组  长度是 定点数的2倍
        //前一半存储 [墙上的点]  后一半存储[挤出的点]
        var allVertices = new Vector3[numBaseVertices * 2];

        //创建所有顶点
        for (int i = 0; i < numBaseVertices; i++)
        {
            //1.处理[墙上的点]
            //把墙上点 转换为 局部坐标 填入数组的前半部分
            allVertices[i] = transform.InverseTransformPoint(baseVerticesWorld[i]);
            //2.处理 [挤压出的点]  计算出 挤压的点的位置 并转换  起点+(方向*距离)
            allVertices[i + numBaseVertices] = transform.InverseTransformPoint(
                baseVerticesWorld[i] + fixedExtrudeDir * platformThickness);
        }
        //以上 生成 并存储了 墙上的顶点 和 挤出的顶点

        //1. 计算 贴墙面 怎么连线
        int[] baseTriangles = TriangulateConvexPolygon(numBaseVertices, 0);
        //2. 计算 朝外面 怎么连线
        int[] extrusTrangles = TriangulateConvexPolygon(numBaseVertices, numBaseVertices);

        //反转[朝外面] 的顺序
        //如果这两个面是同一个顺序 朝外的面 就是背面  物理引擎无法检测   所以反转后 变为了正面
        System.Array.Reverse(extrusTrangles);

        //把 两个面的链接数据存放到一个大列表中
        var triangles = new List<int>();
        triangles.AddRange(baseTriangles);
        triangles.AddRange(extrusTrangles);

        //3.手动缝合 侧面
        //链接 里外两层点  把侧面的缝隙填补上
        for (int i = 0; i < numBaseVertices; i++)
        {
            //算出 四个关键点的编号
            int currentFront = i;
            int nextFront = (i + 1) % numBaseVertices;//取余是为了 最后一个点 链回 0 点
            int currentBack = i + numBaseVertices;
            int nextBack = ((i + 1) % numBaseVertices) + numBaseVertices;

            //画出侧面的三角形
            triangles.Add(currentFront);
            triangles.Add(nextBack);
            triangles.Add(nextFront);

            //第二个三角形
            triangles.Add(currentFront);
            triangles.Add(currentBack);
            triangles.Add(nextBack);

        }

        data.mesh.Clear();
        data.mesh.vertices = allVertices;//填入点
        data.mesh.triangles = triangles.ToArray();//填入连线
        //计算 光照和 包围盒
        data.mesh.RecalculateNormals();
        data.mesh.RecalculateBounds();
        //把生成的网格 给meshCollider
        data.col.sharedMesh = null;
        data.col.sharedMesh = data.mesh;

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
