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
        if (GUILayout.Button("Generate Terrain"))
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

    [Header("Noise parameters")]
    [SerializeField] private float frequency = 1.0f;
    [SerializeField] private float amplitude = 1.0f;
    [SerializeField] private int octaves = 1;


    int mapSizeWithBorder;
    int erosionBrushRadiusInt;
    private Mesh mesh;
    private MeshFilter meshFilter;
    public float MapDimension => 2 * scale;
    public Vector3 Position => holder.transform.position;
    private Texture2D heightMap;

    private void Awake()
    {
        Init();
        ContructMesh();
    }

    private void Init()
    {
        erosionBrushRadiusInt = Mathf.CeilToInt(erosionBrushRadius);
        mapSizeWithBorder = mapSize + 2 * erosionBrushRadiusInt;
        meshFilter = holder.GetComponent<MeshFilter>();
        heightMap = new Texture2D(mapSize, mapSize, TextureFormat.Alpha8, false);
    }

    public void ContructMesh()
    {
        Init();
        Vector3[] verts = new Vector3[mapSize * mapSize];
        int[] triangles = new int[(mapSize - 1) * (mapSize - 1) * 6];
        Vector2[] uv = new Vector2[mapSize * mapSize];
        int t = 0;
        var pixels = heightMap.GetPixels32();
        for (int i = 0; i < mapSize * mapSize; i++)
        {
            int x = i % mapSize;
            int y = i / mapSize;
            int borderedMapIndex = (y + erosionBrushRadiusInt) * mapSizeWithBorder + x + erosionBrushRadiusInt;
            int meshMapIndex = y * mapSize + x;

            Vector2 percent = new Vector2(x / (mapSize - 1f), y / (mapSize - 1f));
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;

            float normalizedHeight = HeightAt(x, y, frequency, amplitude, octaves);
            pixels[i] = new Color32(0, 0, 0, (byte)Mathf.FloorToInt(byte.MaxValue * normalizedHeight));
            pos += Vector3.up * normalizedHeight * elevationScale;
            verts[meshMapIndex] = pos;
            uv[meshMapIndex] = percent;

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
        mesh.uv = uv;
        mesh.RecalculateNormals();
        meshFilter.sharedMesh = mesh;
        holder.sharedMaterial = material;
        //Update height texture
        heightMap.SetPixels32(pixels);
        heightMap.Apply();

        material.SetFloat("_MaxHeight", elevationScale);
    }

    private float HeightAt(int x, int y, float frequency, float amplitude, int octaves)
    {
        float final_val = 0f;
        float u, v;
        for (int i = 0; i < octaves; i++)
        {
            (u, v) = (((float)x) / (mapSize) * frequency, ((float)y) / (mapSize) * frequency);
            final_val += amplitude * Mathf.PerlinNoise(u, v);
            frequency = frequency * 2f;
            amplitude = amplitude / 2f;
        }
        return final_val;
    }
}