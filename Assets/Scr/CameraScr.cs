using UnityEngine;
using UnityEngine.InputSystem;

public class CameraScr : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float edgeSize = 20f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 20f;

    [Header("Map Limits")]
    [SerializeField] private bool useMapLimits = false;
    [SerializeField] private float minX = -50f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minY = -50f;
    [SerializeField] private float maxY = 50f;

    private void Update()
    {
        HandleMovement();
        HandleZoom();
        ApplyLimits();
    }

    private void HandleMovement()
    {
        Vector3 direction = Vector3.zero;

        // =========================
        // WASD
        // =========================

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                direction.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                direction.y -= 1f;

            if (Keyboard.current.dKey.isPressed)
                direction.x += 1f;

            if (Keyboard.current.aKey.isPressed)
                direction.x -= 1f;
        }

        // =========================
        // Рух від краю екрану
        // =========================

        if (Mouse.current != null)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            // Лівий край
            if (mousePosition.x <= edgeSize)
                direction.x -= 1f;

            // Правий край
            if (mousePosition.x >= Screen.width - edgeSize)
                direction.x += 1f;

            // Нижній край
            if (mousePosition.y <= edgeSize)
                direction.y -= 1f;

            // Верхній край
            if (mousePosition.y >= Screen.height - edgeSize)
                direction.y += 1f;
        }

        // Щоб діагональний рух не був швидшим
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        transform.position += direction * moveSpeed * Time.deltaTime;
    }

    private void HandleZoom()
    {
        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        Vector3 position = transform.position;

        // Для 2D камери Orthographic
        Camera camera = GetComponent<Camera>();

        if (camera != null && camera.orthographic)
        {
            camera.orthographicSize -= scroll * zoomSpeed * 0.01f;

            camera.orthographicSize = Mathf.Clamp(
                camera.orthographicSize,
                minZoom,
                maxZoom
            );
        }
    }

    private void ApplyLimits()
    {
        if (!useMapLimits)
            return;

        Vector3 position = transform.position;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        transform.position = position;
    }
}