using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

public class TerrainGenerator : TextureProvider
{
    [SerializeField] private MeshRenderer holder;
    [SerializeField] private Material material;

    [Tooltip("Should be divisible by 16 for the compute shader to work")]
    [SerializeField] private int mapSize = 256;
    [SerializeField] private float scale = 1.0f;

    [SerializeField] private float erosionBrushRadius;

    [Header("Noise parameters")]
    [SerializeField] private float frequency = 1.0f;
    [SerializeField] private float amplitude = 1.0f;
    [SerializeField] private int octaves = 1;
    [Tooltip("The seed for the terrain generation")]
    [SerializeField] private Vector2 offset = Vector2.zero;


    int mapSizeWithBorder;
    int erosionBrushRadiusInt;
    private Mesh mesh;
    private MeshFilter meshFilter;
    public float MapDimension => 2 * scale;
    public Vector3 Position => holder.transform.position;
    private Texture2D heightMap;
    public ComputeShader computeShader;

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

    struct meshData {
        public Vector3 vertex;
        public Vector2 uv;
        public float pixel;
    }

    public void ContructMesh()
    {
        Init();
        int NUM_VERTICES = mapSize * mapSize;
        int NUM_TRIANGLES = (mapSize - 1) * (mapSize - 1) * 6;

        meshData[] meshData = new meshData[NUM_VERTICES];
        int[] triangleData = new int[NUM_TRIANGLES];

        var max = 0f;

        //Create the buffer, and compute shader here
        ComputeBuffer vertexBuffer = new ComputeBuffer(NUM_VERTICES, sizeof(float) * (3+2+1));
        ComputeBuffer triangleBuffer = new ComputeBuffer(NUM_TRIANGLES, sizeof(int));

        vertexBuffer.SetData(meshData);
        triangleBuffer.SetData(triangleData);

        //Setting the buffers
        computeShader.SetBuffer(0, "mesh", vertexBuffer);
        computeShader.SetBuffer(0, "triangles", triangleBuffer);

        //Setting the values
        computeShader.SetInt("mapSize", mapSize);
        computeShader.SetFloat("scale", scale);
        computeShader.SetFloat("frequency", frequency);
        computeShader.SetFloat("amplitude", amplitude);
        computeShader.SetInt("octaves", octaves);
        computeShader.SetFloat("offsetX", offset.x);
        computeShader.SetFloat("offsetY", offset.y);

        computeShader.Dispatch(0, mapSize / 16, mapSize / 16, 1);

        vertexBuffer.GetData(meshData);
        triangleBuffer.GetData(triangleData);

        
        mesh = ComposeMesh(meshData,triangleData);
        meshFilter.sharedMesh = mesh;
        holder.sharedMaterial = material;
        //Update height texture
        float[] pixels = new float[meshData.Length];
        for (int i = 0; i < meshData.Length; i++)
        {
            pixels[i] = meshData[i].pixel;
        }
        max = pixels.Max();
        heightMap.SetPixels32(pixels.Select(c => new Color32(0, 0, 0, (byte)(Mathf.FloorToInt(255 * c / max)))).ToArray());
        heightMap.Apply();

        material.SetFloat("_MaxHeight", amplitude);

        vertexBuffer.Dispose();
        triangleBuffer.Dispose();
    }

    private Mesh ComposeMesh(meshData[] meshData, int[] triangles)
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[meshData.Length];
        Vector2[] uvs = new Vector2[meshData.Length];
        for (int i = 0; i < meshData.Length; i++)
        {
            vertices[i] = meshData[i].vertex;
            uvs[i] = meshData[i].uv;
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
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        return mesh;
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

    public override Texture GetTextureBy(string code)
    {
        return heightMap;
    }
}