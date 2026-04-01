using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class ShadowCasterGroup : MonoBehaviour
{
    private enum CasterSourceType
    {
        MeshFilter,
        SpriteRenderer
    }

    public static readonly List<ShadowCasterGroup> AllGroups = new List<ShadowCasterGroup>();

    [System.Serializable]
    public class CasterItem
    {
        public Transform transform;
        public Vector3[] srcVertices;
        public int[] srcTriangles;
        public Bounds srcBounds;
        public MeshFilter meshFilter;
        public SpriteRenderer spriteRenderer;
        public Sprite sourceSprite;
        public Mesh sourceMesh;
        public bool geometryDirty;

        private CasterSourceType _sourceType;

        public void SetupFromMeshFilter(MeshFilter mf)
        {
            transform = mf.transform;
            meshFilter = mf;
            spriteRenderer = null;
            _sourceType = CasterSourceType.MeshFilter;
            sourceMesh = mf.sharedMesh;
            sourceSprite = null;
            UpdateGeometryFromSource();
        }

        public void SetupFromSpriteRenderer(SpriteRenderer sr)
        {
            transform = sr.transform;
            spriteRenderer = sr;
            meshFilter = null;
            _sourceType = CasterSourceType.SpriteRenderer;
            sourceSprite = sr.sprite;
            sourceMesh = null;
            UpdateGeometryFromSource();
        }

        public void SyncGeometryIfNeeded()
        {
            if (_sourceType == CasterSourceType.MeshFilter)
            {
                Mesh currentMesh = meshFilter != null ? meshFilter.sharedMesh : null;
                if (!ReferenceEquals(currentMesh, sourceMesh))
                {
                    sourceMesh = currentMesh;
                    geometryDirty = true;
                }
            }
            else
            {
                Sprite currentSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
                if (!ReferenceEquals(currentSprite, sourceSprite))
                {
                    sourceSprite = currentSprite;
                    geometryDirty = true;
                }
            }

            if (geometryDirty)
            {
                UpdateGeometryFromSource();
            }
        }

        private void UpdateGeometryFromSource()
        {
            geometryDirty = false;

            if (_sourceType == CasterSourceType.MeshFilter)
            {
                if (sourceMesh == null)
                {
                    srcVertices = System.Array.Empty<Vector3>();
                    srcTriangles = System.Array.Empty<int>();
                    srcBounds = default;
                    return;
                }

                srcVertices = sourceMesh.vertices;
                srcTriangles = sourceMesh.triangles;
                srcBounds = sourceMesh.bounds;
                return;
            }

            if (sourceSprite == null)
            {
                srcVertices = System.Array.Empty<Vector3>();
                srcTriangles = System.Array.Empty<int>();
                srcBounds = default;
                return;
            }

            Vector2[] spriteVerts2D = sourceSprite.vertices;
            ushort[] spriteTris = sourceSprite.triangles;
            Vector3[] spriteVerts3D = new Vector3[spriteVerts2D.Length];
            int[] triangles = new int[spriteTris.Length];

            for (int i = 0; i < spriteVerts2D.Length; i++)
            {
                Vector2 v = spriteVerts2D[i];
                spriteVerts3D[i] = new Vector3(v.x, v.y, 0f);
            }

            for (int i = 0; i < spriteTris.Length; i++)
            {
                triangles[i] = spriteTris[i];
            }

            srcVertices = spriteVerts3D;
            srcTriangles = triangles;
            srcBounds = sourceSprite.bounds;
        }
    }

    public List<CasterItem> Casters { get; private set; } = new List<CasterItem>();

    void Awake()
    {
        InitializeGroup();
    }

    void OnEnable()
    {
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

    [ContextMenu("刷新 Shadow Casters")]
    public void RebuildData()
    {
        InitializeGroup();
    }

    private void InitializeGroup()
    {
        Casters.Clear();

        int shadowCasterLayer = LayerMask.NameToLayer("ShadowCaster");
        MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>(true);
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (var mf in mfs)
        {
            if (mf.gameObject.layer != shadowCasterLayer) continue;
            if (mf.sharedMesh == null) continue;
            if (mf.transform == transform && mfs.Length > 1) continue;

            CasterItem item = new CasterItem();
            item.SetupFromMeshFilter(mf);
            Casters.Add(item);
        }

        foreach (var sr in srs)
        {
            if (sr.gameObject.layer != shadowCasterLayer) continue;
            if (sr.sprite == null) continue;

            CasterItem item = new CasterItem();
            item.SetupFromSpriteRenderer(sr);
            Casters.Add(item);
        }
    }
}
