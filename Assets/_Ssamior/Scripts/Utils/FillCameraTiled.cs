using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class FillCameraTiled : MonoBehaviour
{
    private void Start()
    {
        Camera camera = FindAnyObjectByType<Camera>();

        float worldHeight = 2f * camera.orthographicSize;
        float worldWidth = worldHeight * camera.aspect;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.size = new Vector2(worldWidth, worldHeight);

        // Center it on the camera (keep its own Z for layering)
        Vector3 camPos = camera.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
    }
}