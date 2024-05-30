using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneController : MonoBehaviour
{
    [SerializeField] private TerrainGenerator terrain;
    [SerializeField] private CloudManager cloud;
    private void Awake()
    {
        cloud.Activate(terrain.clouds);
    }
    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            terrain.clouds = !terrain.clouds;
            cloud.Activate(terrain.clouds);
        }
    }
}
