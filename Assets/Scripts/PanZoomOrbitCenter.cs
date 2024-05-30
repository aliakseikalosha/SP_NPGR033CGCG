//MIT License
//Copyright (c) 2023 DA LAB (https://www.youtube.com/@DA-LAB)
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using UnityEngine;

public class PanZoomOrbitCenter : MonoBehaviour
{
    [SerializeField] private Camera orbitCamera;
    [SerializeField] private float rotationSpeed = 500.0f;
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 1f;

    private void Update()
    {
        // Check for Orbit, Pan, Zoom
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.Mouse2))
        {
            CamOrbit();
        }
        if (Input.GetMouseButton(2) && !Input.GetKey(KeyCode.LeftShift))
        {
            Pan();
        }
        Zoom(Input.GetAxis("Mouse ScrollWheel"));
    }

    private Vector2 MouseAxis => new(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

    private void CamOrbit()
    {
        var dir = MouseAxis;
        if (dir != Vector2.zero)
        {
            float horizontalInput = dir.x * rotationSpeed * Time.deltaTime;
            float verticalInput = dir.y * rotationSpeed * Time.deltaTime;
            transform.rotation *= Quaternion.Euler(Vector3.left * verticalInput);
            transform.Rotate(Vector3.up, horizontalInput, Space.World);
        }
    }

    private void Pan()
    {
        var dir = MouseAxis;
        if (dir != Vector2.zero)
        {
            float horizontalInput = dir.x * moveSpeed * Time.deltaTime;
            float verticalInput = dir.y * moveSpeed * Time.deltaTime;
            transform.position += new Vector3(horizontalInput, 0, verticalInput);
        }
    }

    private void Zoom(float zoomDiff)
    {
        if (zoomDiff != 0)
        {
            var dir = (transform.transform.position - orbitCamera.transform.position).normalized;
            orbitCamera.transform.position += Time.deltaTime * zoomDiff * zoomSpeed * dir;
        }
    }
}

