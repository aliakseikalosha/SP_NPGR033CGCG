using UnityEngine;

public class Cloud : MonoBehaviour
{
    [SerializeField] private float speed = 5.0f;
    [SerializeField] private float radius = 1f;
    //ToDo add support for rotation and custom form(?)
    public Vector3 Position => transform.position;

    public bool Moved { get; protected set; }
    public bool Selected { get; set; } = false;
    public float Radius { get; internal set; } = 0.5f;

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
