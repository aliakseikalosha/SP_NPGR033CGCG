using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    private TerrainGenerator terrainGenerator;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Generate Terrain"))
        {
            terrainGenerator.ContructMesh();
        }
    }

    void OnEnable()
    {
        terrainGenerator = (TerrainGenerator)target;
        Tools.hidden = true;
    }

    void OnDisable()
    {
        Tools.hidden = false;
    }
}
#endif

public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] private MeshRenderer holder;
    [SerializeField] private Material material;

    [SerializeField] private int mapSize = 256;
    [SerializeField] private float scale = 1.0f;
    [SerializeField] private float elevationScale = 1.0f;

    [SerializeField] private float erosionBrushRadius;

    int mapSizeWithBorder;
    int erosionBrushRadiusInt;
    private Mesh mesh;
    private MeshFilter meshFilter;
    public float MapSize => mapSize * scale;
    public Vector3 Position => holder.transform.position;

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        erosionBrushRadiusInt = Mathf.CeilToInt(erosionBrushRadius);
        mapSizeWithBorder = mapSize + 2 * erosionBrushRadiusInt;
        meshFilter = holder.GetComponent<MeshFilter>();
    }

    public void ContructMesh()
    {
        Init();
        Vector3[] verts = new Vector3[mapSize * mapSize];
        int[] triangles = new int[(mapSize - 1) * (mapSize - 1) * 6];
        int t = 0;

        for (int i = 0; i < mapSize * mapSize; i++)
        {
            int x = i % mapSize;
            int y = i / mapSize;
            int borderedMapIndex = (y + erosionBrushRadiusInt) * mapSizeWithBorder + x + erosionBrushRadiusInt;
            int meshMapIndex = y * mapSize + x;

            Vector2 percent = new Vector2(x / (mapSize - 1f), y / (mapSize - 1f));
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;

            float normalizedHeight = HeightAt(x, y);
            pos += Vector3.up * normalizedHeight * elevationScale;
            verts[meshMapIndex] = pos;

            // Construct triangles
            if (x != mapSize - 1 && y != mapSize - 1)
            {
                t = (y * (mapSize - 1) + x) * 3 * 2;

                triangles[t + 0] = meshMapIndex + mapSize;
                triangles[t + 1] = meshMapIndex + mapSize + 1;
                triangles[t + 2] = meshMapIndex;

                triangles[t + 3] = meshMapIndex + mapSize + 1;
                triangles[t + 4] = meshMapIndex + 1;
                triangles[t + 5] = meshMapIndex;
            }
        }

        if (mesh)
        {
            mesh.Clear();
        }
        else
        {
            mesh = new Mesh();
        }
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.sharedMesh = mesh;
        holder.sharedMaterial = material;

        material.SetFloat("_MaxHeight", elevationScale);
    }

    private float HeightAt(int x, int y)
    {
        float u, v;
        (u, v) = (((float)x) / mapSize, ((float)y) / mapSize);

        return Mathf.PerlinNoise(u, v);
    }
}