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

    // =========================================================
    // КЕШ ЗАПИТІВ ДО TERRAIN (ОПТИМІЗАЦІЯ)
    // =========================================================
    //
    // GetAllTilesAt викликається сотні-тисячі разів за кадр під час
    // розрахунку дальності руху (бінарний пошук у BattalionScr:
    // GetTerrainMoveCost + IsRoutePassable семплюють ОДНІ Й ТІ Ж
    // точки маршруту незалежно одне від одного). Physics2D.OverlapPointAll
    // — важкий виклик з алокацією масиву, тож без кешування прицілювання
    // наказу дає різкий просад FPS.
    //
    // Кеш прив'язаний до Time.frameCount: у межах одного кадру той самий
    // (округлений) запит повертається з кешу, без повторного фізичного
    // запиту. На наступному кадрі кеш автоматично скидається, тож
    // актуальність даних (якщо terrain колись стане динамічним) не
    // страждає — це лише усуває ПОВТОРНІ запити в межах кадру.
    private int cachedFrame = -1;
    private readonly Dictionary<Vector2Int, List<TerrainTileScr>> tileCache =
        new Dictionary<Vector2Int, List<TerrainTileScr>>();

    // Розмір комірки округлення. Значно менший за типовий крок
    // семплування (terrainSampleStep у BattalionScr), тож на візуал
    // і точність розрахунків це не впливає — лише об'єднує запити,
    // що й так фактично питають про ту саму точку.
    private const float CacheCellSize = 0.05f;

    private static Vector2Int ToCacheKey(Vector2 position)
    {
        return new Vector2Int(
            Mathf.RoundToInt(position.x / CacheCellSize),
            Mathf.RoundToInt(position.y / CacheCellSize));
    }

    public List<TerrainTileScr> GetAllTilesAt(Vector2 position)
    {
        if (Time.frameCount != cachedFrame)
        {
            tileCache.Clear();
            cachedFrame = Time.frameCount;
        }

        Vector2Int key = ToCacheKey(position);

        if (tileCache.TryGetValue(key, out List<TerrainTileScr> cachedTiles))
            return cachedTiles;

        Collider2D[] hits = Physics2D.OverlapPointAll(position, terrainLayer);

        List<TerrainTileScr> tiles = new List<TerrainTileScr>();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            TerrainTileScr tile = hits[i].GetComponentInParent<TerrainTileScr>();

            if (tile == null)
                continue;

            if (!tiles.Contains(tile))
                tiles.Add(tile);
        }

        tileCache[key] = tiles;

        return tiles;
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
            cost *= GetMoveCostMultiplier(tiles[i].type);
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

    // =========================================================
    // LINE OF SIGHT
    // =========================================================

    public bool BlocksLineOfSight(LandscapeType type)
    {
        return type == LandscapeType.Forest || type == LandscapeType.Mountains;
    }

    /// <summary>
    /// Чи блокує ця terrain-плитка лінію вогню САМЕ для стрільця,
    /// що стоїть на плитках зі списку shooterTiles.
    ///
    /// Плитка, на якій стоїть сам стрілець, НІКОЛИ не блокує його
    /// власний постріл — з лісу/гори можна стріляти (дальність вже
    /// зменшується окремо через GetAttackRangeMultiplier). Блокує
    /// лише лінія вогню, що йде В ліс/гору чи КРІЗЬ них — тобто
    /// будь-яка інша лісова/гірська плитка на шляху променя.
    ///
    /// Без цього винятку Physics2D.RaycastAll повертає власну
    /// плитку стрільця як влучення на distance = 0 (промінь
    /// починається всередині її колайдера), і постріл блокувався б
    /// одразу на нульовій дистанції для будь-кого в лісі.
    /// </summary>
    public bool BlocksLineOfSightFrom(TerrainTileScr tile, List<TerrainTileScr> shooterTiles)
    {
        if (tile == null)
            return false;

        if (!BlocksLineOfSight(tile.type))
            return false;

        if (shooterTiles != null && shooterTiles.Contains(tile))
            return false;

        return true;
    }

    // =========================================================
    // VISION / FOG OF WAR
    // =========================================================

    /// <summary>
    /// Множник дальності виявлення (для тумана війни) залежно від
    /// місцевості, на якій стоїть СПОСТЕРІГАЧ. Той самий принцип,
    /// що й GetAttackRangeMultiplier: ліс заважає бачити далеко.
    /// </summary>
    public float GetVisionRangeMultiplier(Vector2 position)
    {
        if (HasTerrainAt(position, LandscapeType.Forest))
            return 1f / 1.5f;

        return 1f;
    }

    /// <summary>
    /// Чи бачить спостерігач у точці "from" ціль у точці "to" —
    /// тобто чи немає між ними суцільної лісової/гірської плитки,
    /// що блокує лінію зору (BlocksLineOfSight). Власна плитка
    /// спостерігача ніколи не блокує його ж огляд (той самий виняток,
    /// що й у BlocksLineOfSightFrom для атаки).
    /// </summary>
    public bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2 offset = to - from;
        float distance = offset.magnitude;

        if (distance <= 0.001f)
            return true;

        Vector2 direction = offset / distance;

        List<TerrainTileScr> shooterTiles = GetAllTilesAt(from);

        RaycastHit2D[] hits = Physics2D.RaycastAll(from, direction, distance, terrainLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;

            if (collider == null)
                continue;

            TerrainTileScr tile = collider.GetComponentInParent<TerrainTileScr>();

            if (BlocksLineOfSightFrom(tile, shooterTiles))
                return false;
        }

        return true;
    }

    // =========================================================
    // ATTACK RANGE
    // =========================================================

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
}