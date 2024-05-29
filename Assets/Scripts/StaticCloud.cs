using UnityEngine;

public class StaticCloud : MonoBehaviour
{
    [SerializeField] protected float speed = 5.0f;
    [SerializeField] protected float radius = 1f;
    [SerializeField] protected float lifeTime = 5;
    protected float life;

    //TODO add support for rotation and custom form(?)
    public Vector3 Position => transform.position;
    public float Radius => radius;
    public Vector3 RandomPositionInside
    {
        get
        {
            var pos = transform.position;
            return pos + new Vector3(Random.value, 0, Random.value) * Radius;
        }
    }
    public float WaterLeft => Mathf.Clamp01(life / lifeTime);

    protected virtual void Awake()
    {
        life = lifeTime;
    }

    protected virtual void Update()
    {
        life -= Time.deltaTime;
        if (life < 0)
        {
            Destroy(gameObject);
        }
    }
}
