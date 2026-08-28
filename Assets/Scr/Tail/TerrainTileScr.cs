using UnityEngine;

public enum LandscapeType
{
    Field,
    Forest,
    Road,
    City,
    Mountains,
    River
}

public class TerrainTileScr : MonoBehaviour
{
    [Header("Terrain")]
    public LandscapeType type = LandscapeType.Field;

    [Header("Height")]
    public int height = 0;

    [Header("Generation")]
    public GameObject[] prefabs;

    public float spacing = 5f;

    [Tooltip("Невеликий випадковий зсув, наприклад 0.1")]
    public float randomOffset = 0.1f;

    [Tooltip("Випадковий поворот по Z")]
    public float randomRotation = 2f;

    [ContextMenu("Generate Landscape")]
    private void GenerateLandscape()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
        {
            Debug.LogError($"TerrainTileScr '{name}' не має Collider2D.", this);
            return;
        }

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError($"TerrainTileScr '{name}' не має Prefabs.", this);
            return;
        }

        // Видаляємо стару генерацію
        Transform oldParent = transform.Find("Generated");
        if (oldParent != null)
        {
            DestroyImmediate(oldParent.gameObject);
        }

        GameObject generated = new GameObject("Generated");
        generated.transform.SetParent(transform);
        generated.transform.localPosition = Vector3.zero;

        Bounds bounds = col.bounds;

        int row = 0;

        for (float y = bounds.min.y; y <= bounds.max.y; y += spacing)
        {
            // Кожен другий ряд зміщений на половину spacing
            float offsetX = (row % 2 == 0) ? 0f : spacing / 2f;

            for (float x = bounds.min.x + offsetX;
                 x <= bounds.max.x;
                 x += spacing)
            {
                Vector2 point = new Vector2(x, y);

                // Точка повинна бути всередині Collider
                if (!col.OverlapPoint(point))
                    continue;

                // Мінімальний рандом
                float offsetXRandom = Random.Range(-randomOffset, randomOffset);
                float offsetYRandom = Random.Range(-randomOffset, randomOffset);

                Vector3 spawnPosition = new Vector3(
                    point.x + offsetXRandom,
                    point.y + offsetYRandom,
                    0f
                );

                // Випадковий prefab
                GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

                // Невеликий випадковий поворот
                Quaternion rotation = Quaternion.Euler(
                    0f,
                    0f,
                    Random.Range(-randomRotation, randomRotation)
                );

                GameObject obj = Instantiate(
                    prefab,
                    spawnPosition,
                    rotation,
                    generated.transform
                );

                // Якщо префаб має локальну систему координат
                obj.transform.localScale = prefab.transform.localScale;
            }

            row++;
        }

        Debug.Log($"Landscape '{name}' успішно згенерований.");
    }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
        {
            Debug.LogError(
                $"TerrainTileScr '{name}' не має Collider2D. " +
                "TerrainManager не зможе визначити terrain.",
                this
            );
        }
    }
}