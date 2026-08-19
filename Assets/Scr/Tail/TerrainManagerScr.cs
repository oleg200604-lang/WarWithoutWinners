using UnityEngine;

public class TerrainManagerScr : MonoBehaviour
{
    public static TerrainManagerScr Instance { get; private set; }

    [Header("Terrain")]
    [SerializeField] private LayerMask terrainLayer;

    public LayerMask TerrainLayer => terrainLayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // ---------------------------------------------------------
    // TILE
    // ---------------------------------------------------------

    public TerrainTileScr GetTileAt(Vector2 position)
    {
        Collider2D hit = Physics2D.OverlapPoint(
            position,
            terrainLayer
        );

        if (hit == null)
            return null;

        return hit.GetComponent<TerrainTileScr>();
    }

    public LandscapeType GetTypeAt(Vector2 position)
    {
        TerrainTileScr tile = GetTileAt(position);

        if (tile == null)
            return LandscapeType.Field;

        return tile.type;
    }

    public int GetHeightAt(Vector2 position)
    {
        TerrainTileScr tile = GetTileAt(position);

        if (tile == null)
            return 0;

        return tile.height;
    }

    // ---------------------------------------------------------
    // MOVEMENT
    // ---------------------------------------------------------

    public float GetMoveCostMultiplier(LandscapeType type)
    {
        switch (type)
        {
            case LandscapeType.Field:
                return 1f;

            case LandscapeType.Road:
                return 0.5f;

            case LandscapeType.Forest:
                return 1.5f;

            case LandscapeType.City:
                return 1f;

            case LandscapeType.Mountains:
                return 2f;

            case LandscapeType.River:
                return 10f;

            default:
                return 1f;
        }
    }

    public float GetMoveCost(Vector2 position)
    {
        LandscapeType type = GetTypeAt(position);

        return GetMoveCostMultiplier(type);
    }

    // ---------------------------------------------------------
    // PASSABILITY
    // ---------------------------------------------------------

    public bool IsPassable(Vector2 position, BattalionType battalionType)
    {
        LandscapeType type = GetTypeAt(position);

        // Артилерія не може заходити в гори та річку.
        if (battalionType == BattalionType.artillery)
        {
            if (type == LandscapeType.Mountains ||
                type == LandscapeType.River)
            {
                return false;
            }
        }

        // Тут можна додати інші обмеження.
        // Наприклад:
        //
        // cavalry -> Mountains
        // mechanically -> River
        // etc.

        return true;
    }

    public bool BlocksArtillery(LandscapeType type)
    {
        return type == LandscapeType.Mountains ||
               type == LandscapeType.River;
    }

    // ---------------------------------------------------------
    // LINE OF SIGHT
    // ---------------------------------------------------------

    public bool BlocksLineOfSight(LandscapeType type)
    {
        return type == LandscapeType.Forest ||
               type == LandscapeType.Mountains;
    }

    // ---------------------------------------------------------
    // ATTACK RANGE
    // ---------------------------------------------------------
    public float GetLineOfSightDistance(
    Vector3 origin,
    Vector3 direction,
    float maxDistance,
    float sampleStep = 0.15f)
    {
        if (maxDistance <= 0f || direction.sqrMagnitude < 0.001f)
            return 0f;

        if (Instance == null)
            return maxDistance;

        direction.Normalize();

        int samples = Mathf.CeilToInt(maxDistance / sampleStep);

        // i = 1: не блокуємо промінь terrain-тайлом,
        // на якому стоїть сам батальйон.
        for (int i = 1; i <= samples; i++)
        {
            float distance = maxDistance * i / samples;
            Vector3 point = origin + direction * distance;

            if (BlocksLineOfSight(GetTypeAt(point)))
                return Mathf.Max(0f, distance - sampleStep);
        }

        return maxDistance;
    }
    public float GetAttackRangeMultiplier(LandscapeType type)
    {
        if (type == LandscapeType.Forest)
            return 1f / 1.5f;

        return 1f;
    }

    public float GetAttackRangeMultiplier(Vector2 position)
    {
        return GetAttackRangeMultiplier(
            GetTypeAt(position)
        );
    }

    // ---------------------------------------------------------
    // DEBUG
    // ---------------------------------------------------------

    public void DebugTerrain(Vector2 position)
    {
        TerrainTileScr tile = GetTileAt(position);

        if (tile == null)
        {
            Debug.Log(
                $"Terrain at {position}: NONE -> Field"
            );

            return;
        }

        Debug.Log(
            $"Terrain at {position}: " +
            $"{tile.type}, height = {tile.height}, " +
            $"move multiplier = {GetMoveCostMultiplier(tile.type)}"
        );
    }
}