using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;

public class CloudManager : TextureProvider
{
    [SerializeField] private StaticCloud prefab;
    [SerializeField] private Cloud[] clouds;
    [SerializeField] private int mapSize = 512;
    [SerializeField] private TerrainGenerator generator;
    [SerializeField] private LayerMask cloudLayer;
    [SerializeField] private LayerMask groundLayer;

    private Texture2D mask;
    private List<StaticCloud> allClouds = new();
    private int lastUsedCloud;
    private float pixelPerUnit => generator.MapDimension / mapSize;
    private bool needUpdate => clouds.Any(c => c.Moved);
    public Texture2D Mask => mask;

    public StaticCloud RandomCloud
    {
        get
        {
            allClouds = allClouds.Where(c => c != null).ToList();
            if (++lastUsedCloud >= allClouds.Count)
            {
                lastUsedCloud = 0;
            }
            return allClouds[lastUsedCloud];
        }
    }


    public void Awake()
    {
        mask = new Texture2D(mapSize, mapSize, TextureFormat.Alpha8, false);
        allClouds.AddRange(clouds);
        UpdateMask();
    }

    public void LateUpdate()
    {
        if (Input.GetMouseButtonUp(0))
        {
            var maxDistance = 1000f;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.yellow, 1);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, cloudLayer))
            {
                Debug.Log($"Hit : {hit.collider.name}", hit.collider);
                var cloud = hit.collider.GetComponentInParent<Cloud>();
                if (cloud)
                {
                    foreach (var c in clouds)
                    {
                        c.Selected = false;
                    }
                    cloud.Selected = true;
                }
                return;
            }
            if (Physics.Raycast(ray, out hit, maxDistance, groundLayer))
            {
                SpawnStaticCloud(ray.origin + ray.direction * hit.distance);
            }
        }
    }

    private void SpawnStaticCloud(Vector3 position)
    {
        position.y = clouds[0].Position.y;
        var c = Instantiate(prefab, position, Quaternion.identity, transform);
        allClouds.Add(c);
    }

    public Vector3 PositonOnTerain(Vector3 pos)
    {
        var cloudPos = pos - generator.Position;
        var posOnTerrain = (cloudPos - (Vector3.zero - Vector3.one * generator.MapDimension / 2)) / generator.MapDimension;
        return posOnTerrain;
    }

    private Vector2Int ConvertToMaskPosition(Cloud cloud)
    {
        var posOnTexture = PositonOnTerain(cloud.Position) * mapSize;
        return new Vector2Int(Mathf.RoundToInt(posOnTexture.x), Mathf.RoundToInt(posOnTexture.z));
    }

    private void UpdateMask()
    {
        mask.Clear();
        foreach (var c in clouds)
        {
            var pos = ConvertToMaskPosition(c);
            var color = Color.white;
            var radius = Mathf.CeilToInt(c.Radius / pixelPerUnit);
            mask = mask.DrawCircle(color, pos.x, pos.y, radius);
        }
        mask.Apply();
    }

    public override Texture GetTextureBy(string code)
    {
        return mask;
    }

    internal void Activate(bool isActive)
    {
        foreach (var cloud in allClouds)
        {
            cloud.gameObject.SetActive(isActive);
        }
        enabled = isActive;
    }
}
public static class Texture2DExt
{
    public static void Clear(this Texture2D texture)
    {
        var pixels = texture.GetPixels32();

        var transparent = new Color32(0, 0, 0, 0);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
    }


    public static Texture2D DrawCircle(this Texture2D tex, Color color, int x, int y, int radius = 3)
    {
        float rSquared = radius * radius;

        for (int u = x - radius; u < x + radius + 1; u++)
            for (int v = y - radius; v < y + radius + 1; v++)
                if ((x - u) * (x - u) + (y - v) * (y - v) < rSquared)
                    tex.SetPixel(u, v, color);

        return tex;
    }
}