using UnityEngine;
using System.Collections.Generic;

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

    public List<TerrainTileScr> GetAllTilesAt(Vector2 position)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(
            position,
            terrainLayer
        );

        List<TerrainTileScr> tiles = new List<TerrainTileScr>();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            TerrainTileScr tile =
                hits[i].GetComponentInParent<TerrainTileScr>();

            if (tile == null)
                continue;

            if (!tiles.Contains(tile))
                tiles.Add(tile);
        }

        return tiles;
    }

    /// <summary>
    /// Повертає перший terrain.
    /// НЕ використовувати для розрахунку руху.
    /// </summary>
    public TerrainTileScr GetTileAt(Vector2 position)
    {
        List<TerrainTileScr> tiles = GetAllTilesAt(position);

        if (tiles.Count == 0)
            return null;

        return tiles[0];
    }

    // =========================================================
    // TERRAIN TYPE
    // =========================================================

    /// <summary>
    /// Перевіряє, чи є конкретний тип terrain у позиції.
    /// Працює навіть якщо поверх нього є інший terrain.
    /// </summary>
    public bool HasTerrainAt(
        Vector2 position,
        LandscapeType type)
    {
        List<TerrainTileScr> tiles = GetAllTilesAt(position);

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].type == type)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Старий метод залишаємо для сумісності.
    /// Він повертає тільки один terrain.
    /// Не використовувати для movement cost.
    /// </summary>
    public LandscapeType GetTypeAt(Vector2 position)
    {
        TerrainTileScr tile = GetTileAt(position);

        if (tile == null)
            return LandscapeType.Field;

        return tile.type;
    }

    // =========================================================
    // HEIGHT
    // =========================================================

    public int GetHeightAt(Vector2 position)
    {
        List<TerrainTileScr> tiles = GetAllTilesAt(position);

        if (tiles.Count == 0)
            return 0;

        int highestHeight = tiles[0].height;

        for (int i = 1; i < tiles.Count; i++)
        {
            if (tiles[i].height > highestHeight)
                highestHeight = tiles[i].height;
        }

        return highestHeight;
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

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

    /// <summary>
    /// ГОЛОВНИЙ метод розрахунку вартості руху.
    ///
    /// Якщо:
    /// Field  = 1
    /// Forest = 1.5
    /// Road   = 0.5
    ///
    /// Forest + Road:
    /// 1.5 * 0.5 = 0.75
    /// </summary>
    public float GetMoveCost(Vector2 position)
    {
        List<TerrainTileScr> tiles = GetAllTilesAt(position);

        // Якщо terrain немає — звичайне поле.
        if (tiles.Count == 0)
            return 1f;

        float cost = 1f;

        for (int i = 0; i < tiles.Count; i++)
        {
            cost *= GetMoveCostMultiplier(
                tiles[i].type
            );
        }

        return cost;
    }

    // =========================================================
    // PASSABILITY
    // =========================================================

    public bool IsPassable(
        Vector2 position,
        BattalionType battalionType)
    {
        List<TerrainTileScr> tiles = GetAllTilesAt(position);

        for (int i = 0; i < tiles.Count; i++)
        {
            LandscapeType type = tiles[i].type;

            if (battalionType == BattalionType.artillery)
            {
                if (type == LandscapeType.Mountains ||
                    type == LandscapeType.River)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool BlocksArtillery(LandscapeType type)
    {
        return type == LandscapeType.Mountains ||
               type == LandscapeType.River;
    }

    // =========================================================
    // LINE OF SIGHT
    // =========================================================

    public bool BlocksLineOfSight(LandscapeType type)
    {
        return type == LandscapeType.Forest ||
               type == LandscapeType.Mountains;
    }

    public float GetLineOfSightDistance(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        float sampleStep = 0.15f)
    {
        if (maxDistance <= 0f ||
            direction.sqrMagnitude < 0.001f)
        {
            return 0f;
        }

        direction.Normalize();

        int samples =
            Mathf.CeilToInt(maxDistance / sampleStep);

        for (int i = 1; i <= samples; i++)
        {
            float distance =
                maxDistance * i / samples;

            Vector3 point =
                origin + direction * distance;

            List<TerrainTileScr> tiles =
                GetAllTilesAt(point);

            for (int j = 0; j < tiles.Count; j++)
            {
                if (BlocksLineOfSight(tiles[j].type))
                {
                    return Mathf.Max(
                        0f,
                        distance - sampleStep
                    );
                }
            }
        }

        return maxDistance;
    }

    // =========================================================
    // ATTACK RANGE
    // =========================================================

    public float GetAttackRangeMultiplier(
        LandscapeType type)
    {
        if (type == LandscapeType.Forest)
            return 1f / 1.5f;

        return 1f;
    }

    public float GetAttackRangeMultiplier(
        Vector2 position)
    {
        // Якщо ліс є хоча б одним із terrain —
        // дальність скорочується.
        if (HasTerrainAt(
            position,
            LandscapeType.Forest))
        {
            return 1f / 1.5f;
        }

        return 1f;
    }

    // =========================================================
    // DEBUG
    // =========================================================

    public void DebugTerrain(Vector2 position)
    {
        List<TerrainTileScr> tiles =
            GetAllTilesAt(position);

        if (tiles.Count == 0)
        {
            Debug.Log(
                $"Terrain at {position}: NONE -> Field"
            );

            return;
        }

        string result =
            $"Terrain at {position}: ";

        float totalCost = 1f;

        for (int i = 0; i < tiles.Count; i++)
        {
            float multiplier =
                GetMoveCostMultiplier(
                    tiles[i].type
                );

            totalCost *= multiplier;

            result +=
                $"{tiles[i].type} × {multiplier}";

            if (i < tiles.Count - 1)
                result += " | ";
        }

        result +=
            $" => TOTAL MOVE COST = {totalCost}";

        Debug.Log(result);
    }
}