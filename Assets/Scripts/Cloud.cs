using UnityEngine;

public class Cloud : MonoBehaviour
{
    [SerializeField] private float speed = 5.0f;
    [SerializeField] private float radius = 1f;
    //ToDo add support for rotation and custom form(?)
    public Vector3 Position => transform.position;

    public bool Moved { get; protected set; }
    public bool Selected { get; set; } = false;
    public float Radius => radius;
    public Vector2 PositionInside
    {
        get
        {
            var pos = transform.position;
            return new Vector2(pos.x, pos.z) + new Vector2(Random.value, Random.value) * Radius;
        }
    }

    public void Update()
    {
        if (Selected)
        {
            var dir = Vector3.zero; 
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                dir.x -= 1;
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                dir.x += 1;
            }
            if (Input.GetKey(KeyCode.UpArrow))
            {
                dir.z += 1;
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                dir.z -= 1;
            }
            if(dir != Vector3.zero)
            {
                transform.position += dir * speed * Time.deltaTime;
                Moved = true;
            }
        }
    }

    public void Released()
    {
        Moved = false;
    }
}
