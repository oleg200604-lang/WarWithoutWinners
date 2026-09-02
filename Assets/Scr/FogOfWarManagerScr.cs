using System.Collections.Generic;
using UnityEngine;

public class FogOfWarManagerScr : MonoBehaviour
{
    public static FogOfWarManagerScr Instance
    {
        get;
        private set;
    }

// =========================================================
// INTEGRATION
// =========================================================

[Header("Integration")]

    [Tooltip(
    "BatalionManagerScr команди гравця. " +
    "Саме його teamID, allyID та enemyID " +
    "визначають відносини для Fog of War."
)]
    public BatalionManagerScr playerManager;

    // =========================================================
    // MAP
    // =========================================================

    [Header("Fog Grid")]

    [Tooltip(
        "Центр області, яку покриває Fog of War."
    )]
    public Vector2 mapCenter =
        Vector2.zero;

    [Tooltip(
        "Розмір області Fog of War."
    )]
    public Vector2 mapSize =
        new Vector2(
            200f,
            200f
        );

    [Tooltip(
        "Розмір однієї комірки Fog."
    )]
    [Min(0.5f)]
    public float cellSize =
        2f;

    // =========================================================
    // VISUAL
    // =========================================================

    [Header("Visual")]

    public Material fogMaterial;

    [Tooltip(
        "Колір повністю невідомої території."
    )]
    public Color unexploredColor =
        new Color(
            0f,
            0f,
            0f,
            0.95f
        );

    [Tooltip(
        "Колір дослідженої, але зараз " +
        "невидимої території."
    )]
    public Color exploredColor =
        new Color(
            0f,
            0f,
            0f,
            0.55f
        );

    public string sortingLayerName =
        "Default";

    public int sortingOrder =
        100;

    // =========================================================
    // UPDATE
    // =========================================================

    [Header("Update")]

    [Min(0.02f)]
    public float updateInterval =
        0.15f;

    // =========================================================
    // INTERNAL
    // =========================================================

    private float visibilityTimer;

    /// <summary>
    /// ЄДИНЕ джерело правди для Fog:
    /// команда гравця + allyID[].
    /// </summary>
    private readonly HashSet<int>
        friendlyTeamIDs =
            new HashSet<int>();

    private readonly List<BattalionScr>
        spottersBuffer =
            new List<BattalionScr>();

    private FogCell[,] cells;

    private int gridWidth;

    private int gridHeight;

    private Transform fogContainer;

    private Sprite fogSprite;

    // =========================================================
    // FOG CELL
    // =========================================================

    private class FogCell
    {
        public Vector2 worldPosition;

        public bool explored;

        public bool visible;

        public SpriteRenderer renderer;
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        RefreshFriendlyTeams();

        CreateFogGrid();

        RecalculateVisibility();
    }

    private void Update()
    {
        visibilityTimer -=
            Time.deltaTime;

        if (visibilityTimer > 0f)
            return;

        visibilityTimer =
            updateInterval;

        // Важливо:
        //
        // allyID[] може бути змінений
        // під час гри, тому оновлюємо
        // список перед розрахунком.
        RefreshFriendlyTeams();

        RecalculateVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // =========================================================
    // TEAM RELATIONS
    // =========================================================

    /// <summary>
    /// Оновлює список команд,
    /// які відкривають Fog of War.
    ///
    /// Використовується ТІЛЬКИ:
    ///
    /// playerManager.teamID
    /// playerManager.allyID[]
    ///
    /// enemyID[] тут навмисно
    /// НЕ додаються.
    /// </summary>
    public void RefreshFriendlyTeams()
    {
        friendlyTeamIDs.Clear();

        if (playerManager == null)
        {
            Debug.LogWarning(
                "FogOfWarManagerScr: " +
                "playerManager не призначений."
            );

            return;
        }

        // ---------------------------------------------
        // Команда самого гравця.
        // ---------------------------------------------

        friendlyTeamIDs.Add(
            playerManager.teamID
        );

        // ---------------------------------------------
        // Союзники гравця.
        // ---------------------------------------------

        if (playerManager.allyID == null)
            return;

        for (int i = 0;
             i < playerManager.allyID.Length;
             i++)
        {
            int allyTeamID =
                playerManager.allyID[i];

            // Власну команду дублювати
            // не потрібно, але HashSet
            // і так захищає від дублікатів.
            friendlyTeamIDs.Add(
                allyTeamID
            );
        }
    }

    /// <summary>
    /// Чи є команда дружньою для гравця
    /// у контексті Fog of War.
    /// </summary>
    public bool IsFriendlyTeam(
        int teamID)
    {
        return friendlyTeamIDs.Contains(
            teamID
        );
    }

    /// <summary>
    /// Чи є команда ворогом гравця.
    ///
    /// Використовується для логіки гри,
    /// але вороги НЕ впливають
    /// на формування Fog.
    /// </summary>
    public bool IsEnemyTeam(
        int teamID)
    {
        if (playerManager == null)
            return false;

        if (playerManager.enemyID == null)
            return false;

        for (int i = 0;
             i < playerManager.enemyID.Length;
             i++)
        {
            if (playerManager.enemyID[i] ==
                teamID)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Нейтральна команда.
    /// </summary>
    public bool IsNeutralTeam(
        int teamID)
    {
        if (IsFriendlyTeam(teamID))
            return false;

        if (IsEnemyTeam(teamID))
            return false;

        return true;
    }

    // =========================================================
    // GRID CREATION
    // =========================================================

    private void CreateFogGrid()
    {
        ClearFogGrid();

        gridWidth =
            Mathf.CeilToInt(
                mapSize.x /
                cellSize
            );

        gridHeight =
            Mathf.CeilToInt(
                mapSize.y /
                cellSize
            );

        cells =
            new FogCell[
                gridWidth,
                gridHeight
            ];

        GameObject containerObject =
            new GameObject(
                "FogOfWarGrid"
            );

        containerObject.transform.SetParent(
            transform
        );

        containerObject.transform.localPosition =
            Vector3.zero;

        fogContainer =
            containerObject.transform;

        CreateFogSprite();

        Vector2 bottomLeft =
            mapCenter -
            mapSize * 0.5f;

        for (int x = 0;
             x < gridWidth;
             x++)
        {
            for (int y = 0;
                 y < gridHeight;
                 y++)
            {
                Vector2 position =
                    bottomLeft +
                    new Vector2(
                        (x + 0.5f) *
                        cellSize,

                        (y + 0.5f) *
                        cellSize
                    );

                FogCell cell =
                    new FogCell();

                cell.worldPosition =
                    position;

                GameObject cellObject =
                    new GameObject(
                        $"Fog_{x}_{y}"
                    );

                cellObject.transform.SetParent(
                    fogContainer
                );

                cellObject.transform.position =
                    new Vector3(
                        position.x,
                        position.y,
                        0f
                    );

                SpriteRenderer renderer =
                    cellObject.AddComponent<
                        SpriteRenderer
                    >();

                renderer.sprite =
                    fogSprite;

                renderer.sortingLayerName =
                    sortingLayerName;

                renderer.sortingOrder =
                    sortingOrder;

                if (fogMaterial != null)
                {
                    renderer.material =
                        fogMaterial;
                }

                cellObject.transform.localScale =
                    new Vector3(
                        cellSize,
                        cellSize,
                        1f
                    );

                renderer.color =
                    unexploredColor;

                cell.renderer =
                    renderer;

                cell.explored =
                    false;

                cell.visible =
                    false;

                cells[x, y] =
                    cell;
            }
        }
    }

    private void ClearFogGrid()
    {
        if (fogContainer != null)
        {
            if (Application.isPlaying)
            {
                Destroy(
                    fogContainer.gameObject
                );
            }
            else
            {
                DestroyImmediate(
                    fogContainer.gameObject
                );
            }
        }

        fogContainer =
            null;

        cells =
            null;
    }

    // =========================================================
    // CREATE SPRITE
    // =========================================================

    private void CreateFogSprite()
    {
        if (fogSprite != null)
            return;

        Texture2D texture =
            new Texture2D(
                1,
                1
            );

        texture.SetPixel(
            0,
            0,
            Color.white
        );

        texture.Apply();

        fogSprite =
            Sprite.Create(
                texture,

                new Rect(
                    0f,
                    0f,
                    1f,
                    1f
                ),

                new Vector2(
                    0.5f,
                    0.5f
                ),

                1f
            );
    }

    // =========================================================
    // VISIBILITY
    // =========================================================

    public void RecalculateVisibility()
    {
        if (cells == null)
            return;

        CollectSpotters();

        ResetVisibility();

        RevealVisibleCells();

        UpdateFogVisuals();

        RecalculateBattalionVisibility();
    }

    // =========================================================
    // COLLECT SPOTTERS
    // =========================================================

    /// <summary>
    /// Тільки батальйони гравця
    /// та його союзників відкривають Fog.
    ///
    /// Вороги та нейтральні
    /// НЕ можуть прибирати Fog.
    /// </summary>
    private void CollectSpotters()
    {
        spottersBuffer.Clear();

        IReadOnlyList<BattalionScr> all =
            BattalionScr.AllActive;

        for (int i = 0;
             i < all.Count;
             i++)
        {
            BattalionScr battalion =
                all[i];

            if (battalion == null)
                continue;

            if (IsFriendlyTeam(
                battalion.teamID
            ))
            {
                spottersBuffer.Add(
                    battalion
                );
            }
        }
    }

    // =========================================================
    // RESET VISIBILITY
    // =========================================================

    private void ResetVisibility()
    {
        for (int x = 0;
             x < gridWidth;
             x++)
        {
            for (int y = 0;
                 y < gridHeight;
                 y++)
            {
                cells[x, y].visible =
                    false;
            }
        }
    }

    // =========================================================
    // REVEAL CELLS
    // =========================================================

    private void RevealVisibleCells()
    {
        for (int i = 0;
             i < spottersBuffer.Count;
             i++)
        {
            BattalionScr spotter =
                spottersBuffer[i];

            if (spotter == null)
                continue;

            RevealFromBattalion(
                spotter
            );
        }
    }

    private void RevealFromBattalion(
        BattalionScr spotter)
    {
        Vector2 origin =
            spotter.transform.position;

        float visionRange =
            spotter.GetEffectiveVisionRange();

        float visionRangeSquared =
            visionRange *
            visionRange;

        for (int x = 0;
             x < gridWidth;
             x++)
        {
            for (int y = 0;
                 y < gridHeight;
                 y++)
            {
                FogCell cell =
                    cells[x, y];

                if (cell.visible)
                    continue;

                Vector2 offset =
                    cell.worldPosition -
                    origin;

                if (offset.sqrMagnitude >
                    visionRangeSquared)
                {
                    continue;
                }

                if (TerrainManagerScr.Instance !=
                    null)
                {
                    if (!TerrainManagerScr.Instance
                        .HasLineOfSight(
                            origin,
                            cell.worldPosition
                        ))
                    {
                        continue;
                    }
                }

                cell.visible =
                    true;

                cell.explored =
                    true;
            }
        }
    }

    // =========================================================
    // UPDATE FOG VISUALS
    // =========================================================

    private void UpdateFogVisuals()
    {
        for (int x = 0;
             x < gridWidth;
             x++)
        {
            for (int y = 0;
                 y < gridHeight;
                 y++)
            {
                FogCell cell =
                    cells[x, y];

                if (cell.renderer == null)
                    continue;

                if (cell.visible)
                {
                    cell.renderer.enabled =
                        false;

                    continue;
                }

                cell.renderer.enabled =
                    true;

                if (cell.explored)
                {
                    cell.renderer.color =
                        exploredColor;
                }
                else
                {
                    cell.renderer.color =
                        unexploredColor;
                }
            }
        }
    }

    // =========================================================
    // BATTALION VISIBILITY
    // =========================================================

    /// <summary>
    /// Визначає, чи видно конкретний батальйон.
    ///
    /// Союзники:
    ///     завжди видимі.
    ///
    /// Вороги:
    ///     видимі тільки якщо їхня
    ///     позиція знаходиться
    ///     у відкритій зоні Fog.
    ///
    /// Нейтральні:
    ///     така сама логіка,
    ///     як у ворогів.
    /// </summary>
    private void RecalculateBattalionVisibility()
    {
        IReadOnlyList<BattalionScr> all =
            BattalionScr.AllActive;

        for (int i = 0;
             i < all.Count;
             i++)
        {
            BattalionScr battalion =
                all[i];

            if (battalion == null)
                continue;

            // ---------------------------------------------
            // Гравець + союзники.
            // Вони ніколи не ховаються.
            // ---------------------------------------------

            if (IsFriendlyTeam(
                battalion.teamID
            ))
            {
                battalion.SetFogVisible(
                    true
                );

                continue;
            }

            // ---------------------------------------------
            // Вороги + нейтральні.
            // Видимі тільки у відкритій зоні.
            // ---------------------------------------------

            bool visible =
                IsWorldPositionVisible(
                    battalion.transform.position
                );

            battalion.SetFogVisible(
                visible
            );
        }
    }

    // =========================================================
    // POSITION VISIBILITY
    // =========================================================

    public bool IsWorldPositionVisible(
        Vector2 position)
    {
        if (cells == null)
            return false;

        Vector2 bottomLeft =
            mapCenter -
            mapSize * 0.5f;

        Vector2 localPosition =
            position -
            bottomLeft;

        int x =
            Mathf.FloorToInt(
                localPosition.x /
                cellSize
            );

        int y =
            Mathf.FloorToInt(
                localPosition.y /
                cellSize
            );

        if (x < 0 ||
            y < 0 ||
            x >= gridWidth ||
            y >= gridHeight)
        {
            return false;
        }

        return cells[x, y].visible;
    }

    // =========================================================
    // PUBLIC REBUILD
    // =========================================================

    [ContextMenu(
        "Rebuild Fog Grid"
    )]
    public void RebuildFogGrid()
    {
        CreateFogGrid();

        RecalculateVisibility();
    }

    [ContextMenu(
        "Refresh Team Relations"
    )]
    public void RefreshTeamRelations()
    {
        RefreshFriendlyTeams();

        RecalculateVisibility();
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                0f,
                1f,
                1f,
                0.5f
            );

        Gizmos.DrawWireCube(
            mapCenter,
            mapSize
        );
    }

}
