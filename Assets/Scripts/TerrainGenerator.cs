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

    [Header("Terrain parameters")]
    [SerializeField] private float frequency = 1.0f;
    [SerializeField] private float amplitude = 1.0f;
    [SerializeField] private int octaves = 1;
    [Tooltip("The seed for the terrain generation")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Header("Erosion parameters")]
    [SerializeField] float inertia = 1f;
    [SerializeField] float capacity = 1f;
    [SerializeField] float deposition = 1f;
    [SerializeField] float erosion = 1f;
    [SerializeField] float evaporation = 1f;
    [SerializeField] float erosionRadius = 1f;
    [SerializeField] float minSplope = 1f;
    [SerializeField] float gravity = 1f;
    [SerializeField] int maxSteps = 1;
    [SerializeField] int numRaindrops = 10000;

    int mapSizeWithBorder;
    int erosionBrushRadiusInt;
    private Mesh mesh;
    private MeshFilter meshFilter;
    public float MapDimension => 2 * scale;
    public Vector3 Position => holder.transform.position;
    private Texture2D heightMap;
    public ComputeShader computeShader;
    private RainDrop[] raindrops;

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
        raindrops = new RainDrop[numRaindrops];
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


    //Everything from here can be moved to a separate class to keep the code a bit cleaner
    private struct RainDrop
    {
        public Vector2 position;
        public Vector2 direction;
        public float velocity;
        public float water;
        public float sediment;
    }

    private Vector2 randomPosition()
    {
        return new Vector2(Random.value, Random.value);
    }

    /// <summary>
    /// Generates the random raindrop positions on the map, and creates the raindrop objects.
    /// TODO: Can be moved to compute shader, but I don't think it is necessary.
    /// </summary>
    private void InitRaindrops()
    {
        for(int i = 0;i<numRaindrops;i++)
        {
            RainDrop raindrop = new RainDrop();
            raindrop.position = randomPosition()*mapSize;
            raindrop.direction = randomPosition().normalized;
            raindrop.sediment = 0;
            raindrop.velocity = 0;
            raindrop.water = 1;
        }
    }

    public float BiLerp(float[] heights, Vector2 position)
    {
        float abu = Mathf.Lerp(heights[0], heights[1], position.x);
        float dcu = Mathf.Lerp(heights[2],heights[3], position.x);
        return Mathf.Lerp(abu, dcu, position.y);
    }

    /// <summary>
    /// Updates the mesh height
    /// </summary>
    public void erodeTerrain()
    {
        
    }

    //Deposit end erode functions return the change in the sediment. Erode returns a negative, deposit returns a positive vaulue
    public float depositSediment()
    {
        return 0f;
    }

    public float gainSediment()
    {
        return 0f;
    }

    float[] getNeighbouringVertexHeights(Vector2 position)
    {
        Vector2[] vertexPos = new Vector2[4];
        vertexPos[0] = new Vector2(Mathf.Floor(position.x), Mathf.Floor(position.y));
        vertexPos[1] = new Vector2(Mathf.Floor(position.x), Mathf.Ceil(position.y));
        vertexPos[2] = new Vector2(Mathf.Ceil(position.x), Mathf.Floor(position.y));
        vertexPos[3] = new Vector2(Mathf.Ceil(position.x), Mathf.Ceil(position.y));

        float[] heights = new float[4];
        heights[0] = heights[(int)vertexPos[0].y * mapSize + (int)vertexPos[0].x]; //h00
        heights[1] = heights[(int)vertexPos[1].y * mapSize + (int)vertexPos[1].x]; //h01
        heights[2] = heights[(int)vertexPos[2].y * mapSize + (int)vertexPos[2].x]; //h10
        heights[3] = heights[(int)vertexPos[3].y * mapSize + (int)vertexPos[3].x]; //h11
        return heights;
    }

    /// <summary>
    /// Does one iteration of one raindrop. This is just to write the function, all raindrops will be calculated on
    /// compute shaders.
    /// </summary>
    private void UpdateDrop(RainDrop droplet, float[] heights)
    {
        //1. Calculate the gradients
        float[] heightsOld = getNeighbouringVertexHeights(droplet.position);

        Vector2 grad = new Vector2((heightsOld[2] - heightsOld[0]) * (1 - droplet.position.y) + (heightsOld[3] - heightsOld[1]) * droplet.position.y,
                                    (heightsOld[1] - heightsOld[0]) * (1 - droplet.position.x) + (heightsOld[3] - heightsOld[2]) * droplet.position.x);

        //2. New direction
        Vector2 newDir = droplet.direction * inertia - grad * (1 - inertia);
        if (newDir == Vector2.zero)
        {
            newDir = randomPosition();
        }
        newDir = newDir.normalized;

        //3. New position
        Vector2 newPos = droplet.position + newDir;

        //4. New height
        
        float oldHeight = BiLerp(heightsOld, droplet.position-Vector2Int.FloorToInt(droplet.position));
        float[] heightsNew = getNeighbouringVertexHeights(newPos);
        float newHeight = BiLerp(heightsNew, newPos - Vector2Int.FloorToInt(newPos));
        float heightDiff = newHeight - oldHeight;

        //5. Based on height difference, gain or deposit sediment (unsure with carry capacity is)
        float dropCapacity = Mathf.Max(-heightDiff, minSplope) * droplet.velocity * droplet.water * capacity;
        float sedimentChange;
        if(heightDiff>=0)
        {
            sedimentChange = depositSediment();
        }
        else
        {
            sedimentChange = gainSediment();
        }

        //6. Adjust speed
        float newVelocity = Mathf.Sqrt(droplet.velocity*droplet.velocity+heightDiff*gravity);

        //7. Evaporate water
        float newWater = droplet.water * (1 - evaporation);

        //8. Update droplet values
        droplet.water = newWater;
        droplet.velocity = newVelocity;
        droplet.position = newPos;
        droplet.direction = newDir;
        droplet.sediment += sedimentChange;
    }
}