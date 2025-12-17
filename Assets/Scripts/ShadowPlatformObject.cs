using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
public class ShadowPlatformObject : MonoBehaviour
{
    public Light lightSource;//光源
    public Transform shadowCaster;//产生影子的物体
    public LayerMask wallLayer;//接受影子

    [Tooltip("生成的物理平台的厚度")]
    public float platformThickness = 0.2f;

    //私有成员变量 存储生成的网格 和碰撞体
    private Mesh platformMesh;
    private MeshCollider platformCollider;

    private void Start()
    {
        //创建新的空网格 并命名
        platformMesh = new Mesh { name = "Shadow Platform Mesh" };
        //告诉物体上的meshfilter 使用这个新的网格
        GetComponent<MeshFilter>().mesh = platformMesh;
        //获取 物体上的 mesh collider物理碰撞组件
        platformCollider = GetComponent<MeshCollider>();
        //讲meshcollider设置为凸体, 成为实心物体//允许它与其他 MeshCollider（包括非凸的）发生碰撞
        platformCollider.convex = true;
        //禁用构建出来的渲染
        GetComponent<MeshRenderer>().enabled = false;
    }

    private void Update()
    {
        UpdateShadowPlatform();//待修改 应该放到 灯光改变的时候调用
    }

    public void UpdateShadowPlatform()
    {
        if(!lightSource || !shadowCaster) return;

        Mesh casterMesh = shadowCaster.GetComponent<MeshFilter>().mesh;//获取障碍物的网格数据
        if (casterMesh == null) return;
        //获取到障碍物的 所有顶点
        Vector3[]vertices = casterMesh.vertices;

        //用来存储 墙上的影子 点
        List<Vector3> baseVerticesWorld = new List<Vector3>();

        //存储 '墙面朝向'(法线)  用于挤出厚度
        Vector3 extrusionDirection = Vector3.zero;

        //存储 射线打中墙的信息
        RaycastHit hitInfo;
        var lightposition = lightSource.transform.position;//记录灯的位置

        //遍历障碍物的每一个顶点
        foreach(var vertex in vertices)
        {
            //1.[坐标转换] 把顶点的'局部坐标' 转换为'世界坐标'
            //因为障碍物可能移动了  所以需要世界中的真实位置
            Vector3 worldVertex = shadowCaster.TransformPoint(vertex);
            //2.计算出 光源->障碍物顶点的 方向
            Vector3 dirFromLight = (worldVertex - lightposition).normalized;
            //3.从光源出发 沿方向射出,直到检测到wallLayer层
            if(Physics.Raycast(lightposition,dirFromLight, out hitInfo, 100f, wallLayer))
            {
                //射到 墙 就把   在[墙上的点] 存进列表
                baseVerticesWorld.Add(hitInfo.point);
                //记一下 墙是朝向哪边的(法线)  决定 网格朝哪个方向 增加厚度
                if(extrusionDirection == Vector3.zero)
                {
                    extrusionDirection = hitInfo.normal;
                }
            }
        }//以上 是存储了 影子在墙上的 顶点

        //顶点不够成为一个面 则清空并退出
        if (baseVerticesWorld.Count < 3)
        {
            platformMesh.Clear();
            return;
        }

        int numBaseVertices = baseVerticesWorld.Count;
        //创建一个新的顶点数组  长度是 定点数的2倍
        //前一半存储 [墙上的点]  后一半存储[挤出的点]
        var allVertices = new Vector3[numBaseVertices*2];

        //创建所有顶点
        for (int i = 0; i < numBaseVertices; i++) {
            //1.处理[墙上的点]
            //把墙上点 转换为 局部坐标 填入数组的前半部分
            allVertices[i] = transform.InverseTransformPoint(baseVerticesWorld[i]);
            //2.处理 [挤压出的点]  计算出 挤压的点的位置 并转换  起点+(方向*距离)
            allVertices[i + numBaseVertices] = transform.InverseTransformPoint(
                baseVerticesWorld[i] + extrusionDirection * platformThickness);
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
            int currentBack = i+numBaseVertices;
            int nextBack = ((i + 1) % numBaseVertices)+ numBaseVertices;

            //画出侧面的三角形
            triangles.Add(currentFront);
            triangles.Add(nextBack);
            triangles.Add(nextFront);

            //第二个三角形
            triangles.Add(currentFront);
            triangles.Add(currentBack);
            triangles.Add(nextBack);

        }

        platformMesh.Clear();
        platformMesh.vertices = allVertices;//填入点
        platformMesh.triangles = triangles.ToArray();//填入连线
        //计算 光照和 包围盒
        platformMesh.RecalculateNormals();
        platformMesh.RecalculateBounds();
        //把生成的网格 给meshCollider
        platformCollider.sharedMesh = platformMesh;

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
