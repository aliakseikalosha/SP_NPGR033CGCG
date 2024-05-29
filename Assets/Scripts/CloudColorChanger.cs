using UnityEngine;

public class CloudColorChanger : MonoBehaviour
{
    [SerializeField] private Color full;
    [SerializeField] private Color empty;
    [SerializeField] private ParticleSystem particle;
    [SerializeField] private StaticCloud cloud;

    private void Update()
    {
        var main = particle.main;
        main.startColor = Color.Lerp(empty, full, cloud.WaterLeft);
    }
}
