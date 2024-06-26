#define GPU // CPU/GPU values

using UnityEngine;
using System.Linq;
using System.Collections.Generic;




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
        if (GUILayout.Button("Erode terrain"))
        {
            terrainGenerator.StartErosion();
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
    [SerializeField] private CloudManager cloudManager;

    [Tooltip("Should be divisible by 16 for the compute shader to work")]
    [SerializeField] private int mapSize = 256;
    [SerializeField] private float scale = 1.0f;

    [Header("Terrain parameters")]
    [SerializeField] private float frequency = 1.0f;
    [SerializeField] private float amplitude = 1.0f;
    [SerializeField] private int octaves = 1;
    [Tooltip("The seed for the terrain generation")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Header("Erosion parameters")]
    [SerializeField] public bool clouds = true;
    [Tooltip("How much the inertia influences the direction set up by the gradient")]
    [SerializeField] private float inertia = 0.1f;
    [Tooltip("Controls how much sediment can the water carry")]
    [SerializeField] private float capacity = 10f;
    [SerializeField] private float deposition = 0.3f;
    [SerializeField] private float erosion = 0.3f;
    [SerializeField] private float evaporation = 0.001f;
    [SerializeField] private int erosionRadius = 5;
    [SerializeField] private float minSlope = 0.1f;
    [SerializeField] private float gravity = 20;
    [SerializeField] private int maxSteps = 30;
    [SerializeField] private int numRaindrops = 10000;

    int mapSizeWithBorder;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    public float MapDimension => 2 * scale;
    public Vector3 Position => holder.transform.position;
    private float[] heightMap; //Only used for storing the heights, but I don't think a Texture2D is needed for that
    public ComputeShader heightGenerator;
    public ComputeShader erosionSimulator;
    private RainDrop[] raindrops;
    private float[] weights; //Precalculated weights are stored here
    private Vector2[] raindropPath;

    //Set up compute buffers
    ComputeBuffer raindropsBuffer;
    ComputeBuffer weightsBuffer;
    ComputeBuffer heightmapBuffer;
    ComputeBuffer raindropPathBuffer;

    private void Awake()
    {
        Init();
        ContructMesh();
    }

    private void Start()
    {
        raindrops = new RainDrop[numRaindrops];
        InitRaindrops();
        InitWeights();
        InitBuffers();
    }

    private void FixedUpdate()
    {
        if(Time.frameCount % 5 == 0)
        {
            ErodeTerrainGPU();
            UpdateDropletsVisuals();
            //Change height of the terrain
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].y = heightMap[i];
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
        }
    }

    private void Init()
    {
        mapSizeWithBorder = mapSize + 2 * erosionRadius;
        meshFilter = holder.GetComponent<MeshFilter>();
        meshCollider = holder.GetComponent<MeshCollider>();
        heightMap = new float[mapSize * mapSize];
        weights = new float[(erosionRadius * 2 + 1) * (erosionRadius * 2 + 1)];
        raindropPath = new Vector2[numRaindrops * maxSteps];
        InitWaterTexture();
    }

    struct meshData
    {
        public Vector3 vertex;
        public Vector2 uv;
        public float pixel;
    }

    public void InitBuffers()
    {
        raindropsBuffer = new ComputeBuffer(numRaindrops, sizeof(float) * 8 + 2 * sizeof(int));
        weightsBuffer = new ComputeBuffer((erosionRadius * 2 + 1) * (erosionRadius * 2 + 1), sizeof(float));
        heightmapBuffer = new ComputeBuffer(mapSize * mapSize, sizeof(float));
        raindropPathBuffer = new ComputeBuffer(numRaindrops * maxSteps, sizeof(float) * 2);

        erosionSimulator.SetBuffer(0, "weights", weightsBuffer);
        erosionSimulator.SetBuffer(0, "heightMap", heightmapBuffer);
        erosionSimulator.SetBuffer(0, "raindrops", raindropsBuffer);
        erosionSimulator.SetBuffer(0, "raindropPath", raindropPathBuffer);

        weightsBuffer.SetData(weights);

        erosionSimulator.SetInt("mapSize", mapSize);
        erosionSimulator.SetFloat("inertia", inertia);
        erosionSimulator.SetFloat("capacity", capacity);
        erosionSimulator.SetFloat("deposition", deposition);
        erosionSimulator.SetFloat("erosion", erosion);
        erosionSimulator.SetFloat("evaporation", evaporation);
        erosionSimulator.SetInt("erosionRadius", erosionRadius);
        erosionSimulator.SetFloat("minSplope", minSlope);
        erosionSimulator.SetFloat("gravity", gravity);
        erosionSimulator.SetInt("maxSteps", maxSteps);
    }

    public void ErodeTerrainGPU()
    {
        //Set variables
        InitRaindrops();
        System.Array.Clear(raindropPath, 0, raindropPath.Length);
        raindropPathBuffer.SetData(raindropPath);
        raindropsBuffer.SetData(raindrops);
        heightmapBuffer.SetData(heightMap);

        erosionSimulator.Dispatch(0, numRaindrops / 10, 1, 1);

        //Get data
        heightmapBuffer.GetData(heightMap);
        raindropPathBuffer.GetData(raindropPath);
    }

    //TODO: Move to compute shader
    public void StartErosion()
    {
        raindrops = new RainDrop[numRaindrops];
        InitWeights();
#if GPU
        InitBuffers();
        ErodeTerrainGPU();


#elif CPU
        for (int i = 0; i < numRaindrops; i++)
        {
            //This will be moved to compute shader
            bool alive = true;
            for (int j = 0; j < maxSteps; j++)
            {
                alive = UpdateDrop(ref raindrops[i]);
                if (!alive)
                    break;
            }
        }
#endif
        UpdateDropletsVisuals();


        //Change height of the terrain
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y = heightMap[i];
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }

    public void ContructMesh()
    {
        Init();
        int NUM_VERTICES = mapSize * mapSize;
        int NUM_TRIANGLES = (mapSize - 1) * (mapSize - 1) * 6;

        meshData[] meshData = new meshData[NUM_VERTICES];
        int[] triangleData = new int[NUM_TRIANGLES];

        //Create the buffer, and compute shader here
        ComputeBuffer vertexBuffer = new ComputeBuffer(NUM_VERTICES, sizeof(float) * (3 + 2 + 1));
        ComputeBuffer triangleBuffer = new ComputeBuffer(NUM_TRIANGLES, sizeof(int));

        vertexBuffer.SetData(meshData);
        triangleBuffer.SetData(triangleData);

        //Setting the buffers
        heightGenerator.SetBuffer(0, "mesh", vertexBuffer);
        heightGenerator.SetBuffer(0, "triangles", triangleBuffer);

        //Setting the values
        heightGenerator.SetInt("mapSize", mapSize);
        heightGenerator.SetFloat("scale", scale);
        heightGenerator.SetFloat("frequency", frequency);
        heightGenerator.SetFloat("amplitude", amplitude);
        heightGenerator.SetInt("octaves", octaves);
        heightGenerator.SetFloat("offsetX", offset.x);
        heightGenerator.SetFloat("offsetY", offset.y);

        heightGenerator.Dispatch(0, mapSize / 16, mapSize / 16, 1);

        vertexBuffer.GetData(meshData);
        triangleBuffer.GetData(triangleData);

        ComposeMesh(meshData, triangleData);
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
        holder.sharedMaterial = material;
        //Update height texture
        float[] pixels = new float[meshData.Length];
        for (int i = 0; i < meshData.Length; i++)
        {
            pixels[i] = meshData[i].pixel;
        }
        //Move heightmap to the 0-1 range
        heightMap = pixels;
        material.SetFloat("_Amplitude", amplitude);

        vertexBuffer.Dispose();
        triangleBuffer.Dispose();
    }

    private void ComposeMesh(meshData[] meshData, int[] triangles)
    {
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
    }

    #region Droplet Visuals
    // could probably be moved to standalone script

    // Possible optimalization: Bath rain droplets spawned during same tick together, they have same color and same
    // TTL.

    /// <summary>
    /// Represent a visual for the path of one droplet.
    /// </summary>
    struct DropletPathVisual
    {
        /// <summary>
        /// Contains positions of all pixels in water texture which are affected by this path.
        /// </summary>
        public List<Vector2Int> Pixels;
        /// <summary>
        /// How much seconds remains until path fades.
        /// </summary>
        public float TimeToLive;
    }

    /// <summary>
    /// Contains all visuals which are currently active (are visible).
    /// </summary>
    private List<DropletPathVisual> dropletVisuals = new();
    /// <summary>
    /// Texture with water visualisation.
    /// </summary>
    private Texture2D waterTexture;

    /// <summary>
    /// Determine if water visualisation should be enabled.
    /// </summary>
    [Tooltip("Determine if water visualisation should be enabled.")]
    [SerializeField]
    private bool waterVisualisationEnabled = true;
    /// <summary>
    /// Maximum number of visualized droplets (their whole pathes).
    /// </summary>
    [Tooltip("Maximum number of visualized droplets (their whole pathes).")]
    [SerializeField]
    private int maxDropletVisuals = 100;
    /// <summary>
    /// Maximum number of spawned visualized droplets per tick.
    /// </summary>
    [Tooltip("Maximum number of spawned visualized droplets per tick.")]
    [SerializeField]
    private int maxDropletVisualsPerTick = 1;
    /// <summary>
    /// Width of droplet path visual.
    /// </summary>
    [Tooltip("Width of droplet path visual.")]
    [SerializeField]
    private int visualDropletSize = 10;
    /// <summary>
    /// Number of seconds before droplet visual fade away.
    /// </summary>
    [Tooltip("Number of seconds before droplet visual fade away.")]
    [SerializeField]
    private float dropletVisualsLifetime = 3.0f;
    /// <summary>
    /// Color of water droplet visuals.
    /// </summary>
    [Tooltip("Color of water droplet visuals.")]
    [SerializeField]
    private Color waterColor = Color.blue;

    private void InitWaterTexture()
    {
        waterTexture = new Texture2D(mapSize, mapSize);
        waterTexture.SetPixels(Enumerable.Repeat(Color.black, mapSize * mapSize).ToArray());
        waterTexture.Apply();

        material.SetTexture("_WaterTexture", waterTexture);
    }

    /// <summary>
    /// Update visualisation of water droplets.
    /// </summary>
    private void UpdateDropletsVisuals()
    {
        // We cant remove droplets while we are processing them so we instead store indices of droplets to remove.
        List<int> toRemove = new();

        for (int i = 0; i < dropletVisuals.Count; i++)
        {
            DropletPathVisual visual = dropletVisuals[i];

            visual.TimeToLive -= Time.deltaTime;
            if (visual.TimeToLive < 0.0f)
            {
                toRemove.Add(i);
                visual.TimeToLive = 0.0f;
            }

            dropletVisuals[i] = visual;
        }

        SpawnDropletVisuals();
        UpdateWaterTexture();

        // Remove visuals by swapping them with last items and then remove last items (performance reasons).
        for (int i = 0; i < toRemove.Count; i++)
        {
            dropletVisuals[toRemove[i]] = dropletVisuals[dropletVisuals.Count - 1 - i];
        }
        dropletVisuals.RemoveRange(dropletVisuals.Count - toRemove.Count, toRemove.Count);
    }

    /// <summary>
    /// Spawn new water droplet visuals. Will return immedietly if water visualisation is disabled or maximum number of
    /// droplet visuals has been reached. 
    /// </summary>
    private void SpawnDropletVisuals()
    {
        if (!waterVisualisationEnabled || dropletVisuals.Count >= maxDropletVisuals)
            return;

        for (int dropletIndex = 0; dropletIndex < numRaindrops; dropletIndex++)
        {
            if (dropletVisuals.Count >= maxDropletVisuals || dropletIndex >= maxDropletVisualsPerTick)
                break;

            DropletPathVisual visual = new();
            visual.Pixels = new List<Vector2Int>();
            visual.TimeToLive = dropletVisualsLifetime;

            for (int stepIndex = 0; stepIndex < maxSteps; stepIndex++)
            {
                int pathIndex = dropletIndex * maxSteps + stepIndex;
                Vector2Int dropletPosition = Vector2Int.RoundToInt(raindropPath[pathIndex]);

                if (dropletPosition == Vector2Int.zero)
                    break;

                // There is probably an overlap between current step and previous one, but we use very small sizes so
                // its not that big problem.
                for (int x = -visualDropletSize / 2; x < visualDropletSize / 2; x++)
                {
                    for (int y = -visualDropletSize / 2; y < visualDropletSize / 2; y++)
                    {
                        Vector2Int pixelPosition = dropletPosition + new Vector2Int(x, y);
                        visual.Pixels.Add(pixelPosition);
                    }
                }
            }

            dropletVisuals.Add(visual);
        }
    }

    /// <summary>
    /// Update water visualisation texture.
    /// </summary>
    private void UpdateWaterTexture()
    {
        foreach (var visual in dropletVisuals)
        {
            Color color = Color.Lerp(Color.black, waterColor, visual.TimeToLive / dropletVisualsLifetime);

            foreach (var position in visual.Pixels)
            {
                waterTexture.SetPixel(position.x, position.y, color);
            }
        }
        waterTexture.Apply();
    }
    #endregion

    #region Rainrop simulation
    //Everything from here can be moved to a separate class to keep the code a bit cleaner
    public struct RainDrop
    {
        public Vector2 position;
        public Vector2 direction;
        public float velocity;
        public float water;
        public float sediment;
        public Vector2Int gridPoint;
        public float height;
    }

    public void InitWeights()
    {
        int size = erosionRadius * 2 + 1;
        float sum = 0;
        for (int y = -erosionRadius; y <= erosionRadius; y++)
        {
            for (int x = -erosionRadius; x <= erosionRadius; x++)
            {
                Vector2 offset = new Vector2(x, y);
                int weightsOffsetIndex = (y + erosionRadius) * size + (x + erosionRadius);
                float w = (Mathf.Max(0.0f, erosionRadius - offset.magnitude));
                weights[weightsOffsetIndex] = w;
                sum += w;
            }
        }
        weights = weights.Select(c => c / sum).ToArray();
    }

    private Vector2 RandomPosition()
    {
        return new Vector2(Random.value, Random.value);
    }

    private Vector2 RandomSpawnRaindrop
    {
        get
        {
            var pos = cloudManager.RandomCloud.RandomPositionInside;
            var uvPos = cloudManager.PositonOnTerain(pos);
            return new Vector2(Mathf.Clamp01(uvPos.x), Mathf.Clamp01(uvPos.z));
        }
    }

    private void InitRaindrops()
    {
        for (int i = 0; i < numRaindrops; i++)
        {
            var pos = (clouds ? RandomSpawnRaindrop : RandomPosition()) * (mapSize - 2);
            float[] heights = GetNeighbouringVertexHeights(pos);
            raindrops[i] = new RainDrop
            {
                position = pos,
                gridPoint = Vector2Int.FloorToInt(pos),
                direction = Vector2.zero,
                sediment = 0,
                velocity = 5,
                water = 1,
                height = BiLerp(heights, pos - Vector2Int.FloorToInt(pos)),
            };
        }
    }

    public float BiLerp(float[] heights, Vector2 position)
    {
        float abu = Mathf.Lerp(heights[0], heights[1], position.x);
        float dcu = Mathf.Lerp(heights[2], heights[3], position.x);
        return Mathf.Lerp(abu, dcu, position.y);
    }

    /// <summary>
    /// Returns a number between 0 and 1, which determines how much the terrain erodes at the position.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public float SoilHardness(Vector3 position)
    {
        return 1f;
    }

    /// <summary>
    /// Update the heightmap
    /// </summary>
    public void ErodeTerrain(Vector2 position, float sedimentChange)
    {
        int side = 2 * erosionRadius + 1;
        Vector2Int gridPos = Vector2Int.RoundToInt(position); //Choose the closest gridpoint
        for (int y = -erosionRadius; y <= erosionRadius; y++)
        {
            for (int x = -erosionRadius; x <= erosionRadius; x++)
            {
                int gridX = gridPos.x + x;
                int gridY = gridPos.y + y;
                int gridIndex = gridY * mapSize + gridX;
                if (gridIndex < mapSize * mapSize && gridIndex >= 0)
                {
                    float weightedSediment = weights[(y + erosionRadius) * side + (x + erosionRadius)] * sedimentChange;
                    float heightChange = (heightMap[gridIndex] < weightedSediment) ? (heightMap[gridIndex]) : weightedSediment;
                    heightMap[gridIndex] -= heightChange;
                }
            }
        }
    }

    public void Deposit(Vector2 position, float sedimentChange)
    {
        Vector2Int flooredPos = Vector2Int.FloorToInt(position);
        Vector2 tilePos = position - flooredPos;
        float[] weights = new float[] {
            (1-tilePos.x)*(1-tilePos.y),
            tilePos.x*(1-tilePos.y),
            (1-tilePos.x)*tilePos.y,
            tilePos.x*tilePos.y
        };

        Vector2[] neighbouringPositions = GetNeighbouringVertexPositions(position);

        for (int i = 0; i < 4; i++)
        {
            int x = (int)neighbouringPositions[i].x;
            int y = (int)neighbouringPositions[i].y;
            heightMap[y * mapSize + x] += weights[i] * sedimentChange;
        }
    }

    public Vector2[] GetNeighbouringVertexPositions(Vector2 position)
    {
        Vector2Int gridPoint = Vector2Int.FloorToInt(position);
        Vector2[] vertexPos = new Vector2[4];
        vertexPos[0] = new Vector2(gridPoint.x, gridPoint.y);
        vertexPos[1] = new Vector2(gridPoint.x, gridPoint.y + 1);
        vertexPos[2] = new Vector2(gridPoint.x + 1, gridPoint.y);
        vertexPos[3] = new Vector2(gridPoint.x + 1, gridPoint.y + 1);
        return vertexPos;
    }

    public float[] GetNeighbouringVertexHeights(Vector2 position)
    {
        Vector2[] vertexPos = GetNeighbouringVertexPositions(position);

        float[] heights = new float[4];
        for (int i = 0; i < 4; i++)
        {
            heights[i] = heightMap[(int)vertexPos[i].y * mapSize + (int)vertexPos[i].x]; //h00 h01 h10 h11
        }
        return heights;
    }

    /// <summary>
    /// Does one iteration of one raindrop. This is just to write the function, all raindrops will be calculated on
    /// compute shaders.
    /// </summary>
    private bool UpdateDrop(ref RainDrop droplet)
    {
        //1. Calculate the gradients
        float[] heightsOld = GetNeighbouringVertexHeights(droplet.position);

        Vector2 dropletOffset = droplet.position - droplet.gridPoint;
        Vector2 grad = new Vector2((heightsOld[2] - heightsOld[0]) * (1 - dropletOffset.y) + (heightsOld[3] - heightsOld[1]) * dropletOffset.y,
                                    (heightsOld[1] - heightsOld[0]) * (1 - dropletOffset.x) + (heightsOld[3] - heightsOld[2]) * dropletOffset.x);
        //2. New direction
        Vector2 newDir = droplet.direction * inertia - grad * (1 - inertia);
        if (newDir == Vector2.zero)
        {
            newDir = RandomPosition();
        }
        newDir = newDir.normalized;

        //3. New position, stop if it flowed off the map, or if it is not moving.
        Vector2 newPos = droplet.position + newDir;
        if (newDir == Vector2.zero || newPos.x < 0 || newPos.x >= mapSize - 1 || newPos.y < 0 || newPos.y >= mapSize - 1)
        {
            return false;
        }

        //4. New height
        float[] heightsNew = GetNeighbouringVertexHeights(newPos);
        float newHeight = BiLerp(heightsNew, newPos - Vector2Int.FloorToInt(newPos));
        float heightDiff = newHeight - droplet.height;
        //5. Based on height difference, gain or deposit sediment
        float sedimentChange;
        float carryCapacity = Mathf.Max(-heightDiff, minSlope) * droplet.velocity * droplet.water * capacity;
        if (heightDiff >= 0 || droplet.sediment > carryCapacity)
        {
            sedimentChange = heightDiff >= 0 ? Mathf.Min(heightDiff, droplet.sediment) : (droplet.sediment - carryCapacity) * deposition; ;
            Deposit(droplet.position, sedimentChange);
            droplet.sediment -= sedimentChange;
        }

        else
        {
            sedimentChange = Mathf.Min((carryCapacity - droplet.sediment) * erosion, -heightDiff);
            ErodeTerrain(droplet.position, sedimentChange);
            droplet.sediment += sedimentChange;
        }

        //6. Adjust speed
        float newVelocity = Mathf.Sqrt(droplet.velocity * droplet.velocity - heightDiff * gravity);

        //7. Evaporate water
        float newWater = droplet.water * (1 - evaporation);
        if (newWater == 0)
        {
            return false;
        }

        //8. Update droplet values
        droplet.water = newWater;
        droplet.velocity = newVelocity;
        droplet.position = newPos;
        droplet.direction = newDir;
        droplet.height = newHeight;
        droplet.gridPoint = Vector2Int.FloorToInt(droplet.position);
        return true;
    }
    #endregion

}