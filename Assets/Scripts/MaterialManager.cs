using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialManager : MonoBehaviour
{
    public Data[] targets;

    public void Start()
    {
        foreach (var t in targets)
        {
            t.Init();
            t.Update();
        }
    }

    [Serializable]
    public class Data
    {
        [SerializeField] private TextureProvider source;
        [SerializeField] private string code;
        [SerializeField] private string mapName;
        [SerializeField] private Material[] material;

        public void Init()
        {
            source.OnTextureUpdate += Update;
        }

        public void Update()
        {
            foreach (var m in material)
            {
                m.SetTexture(mapName, source.GetTextureBy(code));
            }
        }
    }
}


public abstract class TextureProvider : MonoBehaviour
{
    public event Action OnTextureUpdate;
    public abstract Texture GetTextureBy(string code);

    protected void Raise()
    {
        OnTextureUpdate?.Invoke();
    }
}
