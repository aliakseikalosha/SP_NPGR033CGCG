using UnityEngine;
using System.Linq;

public class CloudManager : MonoBehaviour
{
    [SerializeField] private Cloud[] clouds;
    [SerializeField] private int mapSize = 512;
    [SerializeField] private TerrainGenerator generator;
    private Texture2D mask;
    private float pixelPerUnit => mapSize / generator.MapSize;
    private bool needUpdate => clouds.Any(c => c.Moved);

    public void Awake()
    {
        mask = new Texture2D(mapSize, mapSize, TextureFormat.Alpha8, false);
    }

    public void LateUpdate()
    {
        if (needUpdate)
        {
            UpdateMask();
            foreach (var cloud in clouds)
            {
                cloud.Released();
            }
        }
    }

    private Vector2Int ConvertToMaskPosition(Cloud cloud)
    {
        var pos = Vector2Int.zero;
        var cloudPos = cloud.Position - generator.Position;
        return pos;
    }

    private void UpdateMask()
    {
        mask.Clear();
        foreach (var c in clouds)
        {
            var pos = ConvertToMaskPosition(c);
            mask.DrawCircle(Color.white, pos.x, pos.y, Mathf.RoundToInt(c.Radius / pixelPerUnit));
        }
    }
}
public static class Texture2DExt
{
    public static void Clear(this Texture2D texture)
    {
        Color[] pixels = texture.GetPixels(0, 0, texture.width, texture.height, 0);

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] == Color.black)
            {
                pixels[i] = Color.green;
            }
        }
        texture.SetPixels(0, 0, texture.width, texture.height, pixels, 0);
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