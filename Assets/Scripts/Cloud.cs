using System.Runtime.CompilerServices;
using UnityEngine;

public class Cloud : StaticCloud
{
    [SerializeField] private KeyCode[] moveLeft = new[] { KeyCode.A, KeyCode.LeftArrow };
    [SerializeField] private KeyCode[] moveRight = new[] { KeyCode.D, KeyCode.RightArrow };
    [SerializeField] private KeyCode[] moveForward = new[] { KeyCode.W, KeyCode.UpArrow };
    [SerializeField] private KeyCode[] moveBackward = new[] { KeyCode.S, KeyCode.DownArrow };

    public bool Moved { get; protected set; }
    public bool Selected { get; set; } = false;

    protected override void Update()
    {
        if (Selected)
        {
            var dir = Vector3.zero;
            dir += CheckIfPressed(moveLeft, Vector3.left);
            dir += CheckIfPressed(moveRight, Vector3.right);
            dir += CheckIfPressed(moveForward, Vector3.forward);
            dir += CheckIfPressed(moveBackward, Vector3.back);
            if (dir != Vector3.zero)
            {
                transform.position += dir * speed * Time.deltaTime;
                Moved = true;
            }
        }
    }

    private Vector3 CheckIfPressed(KeyCode[] keys, Vector3 dir)
    {
        foreach (var key in keys)
        {
            if (Input.GetKey(key))
            {
                return dir;
            }
        }
        return Vector3.zero;
    }

    public void Released()
    {
        Moved = false;
    }
}
