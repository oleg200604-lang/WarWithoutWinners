using UnityEngine;

public enum LandscapeType
{
    Field, Forest, Road, City, Mountains, River
}

public class TerrainTileScr : MonoBehaviour
{
    [Header("Terrain")]
    public LandscapeType type = LandscapeType.Field;

    [Header("Height")]
    public int height = 0;

    private void Awake()
    {
        // Перевірка, щоб одразу було видно,
        // якщо тайл неправильно налаштований.
        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
        {
            Debug.LogError($"TerrainTileScr '{name}' не має Collider2D. " + "TerrainManager не зможе визначити terrain.", this);
        }
    }
}