using UnityEngine;

#if UNITY_EDITOR
public class Cloud : MonoBehaviour
{
    [SerializeField] private float speed = 5.0f;
    //ToDo add support for rotation and custom form(?)
    public Vector3 Position => transform.position;

    public bool Moved { get; protected set; }
    public bool Selected { get; protected set; }
    public float Radius { get; internal set; } = 1f;

    public void Update()
    {
        if (Selected)
        {
            var dir = Vector3.zero; 
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                dir.x -= 1;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                dir.x += 1;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                dir.z += 1;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                dir.z -= 1;
            }
            if(dir != Vector3.zero)
            {
                transform.position += dir * speed;
                Moved = true;
            }
        }
    }

    public void OnMouseDown()
    {
        Selected = !Selected;
    }

    public void Released()
    {
        Moved = false;
    }
}
