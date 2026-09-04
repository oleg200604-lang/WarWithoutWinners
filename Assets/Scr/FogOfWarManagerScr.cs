using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Туман війни.
///
/// АРХІТЕКТУРА (важливо):
///
/// Раніше видимість батальйону зберігалась як ОДНЕ спільне поле
/// (BattalionScr.fogVisible) — тобто батальйон фізично не міг бути
/// одночасно "видимим" для однієї команди і "невидимим" для іншої.
/// Будь-яка помилка в тому, хто/коли це поле виставляє, ламала
/// картину для всіх одразу.
///
/// Тепер це розділено на дві незалежні речі:
///
/// 1) РЕНДЕР (те, що фізично намальовано на екрані) — рахується
///    ЛИШЕ з точки зору playerManager (локальний глядач/гравець).
///    Це те, що керує сірою заливкою на мапі (FogCell) і тим,
///    показується/ховається сам спрайт батальйону
///    (BattalionScr.SetFogVisible) — як і раніше, бо екран один.
///
/// 2) ЛОГІЧНИЙ ЗАПИТ "чи бачить команда X батальйон Y" —
///    IsVisibleTo(viewer, target). Рахується "на льоту" з ВЛАСНОГО
///    стану видимості кожної команди (TeamVisionState), незалежно
///    від того, що зараз намальовано на екрані. Це дозволяє AI
///    ворога чи союзника питати про власну видимість, не
///    конфліктуючи з рендером гравця і не залежачи від нього.
///    Один і той самий батальйон може бути true для однієї команди
///    і false для іншої — одночасно, без спільного прапорця.
///
/// Геометрія клітинок (позиції) спільна для всіх команд — рахується
/// один раз у CreateFogGrid(). Відрізняється лише те, ЯКІ клітинки
/// кожна команда бачить.
/// </summary>
public class FogOfWarManagerScr : MonoBehaviour
{
    public static FogOfWarManagerScr Instance { get; private set; }

    // =========================================================
    // INTEGRATION
    // =========================================================

    [Header("Integration")]

    [Tooltip(
        "BatalionManagerScr, з чиєї точки зору малюється туман " +
        "НА ЕКРАНІ (сіра заливка + приховування ворожих спрайтів). " +
        "Для логічних запитів \"чи бачить X батальйон Y\" з точки " +
        "зору БУДЬ-ЯКОЇ іншої команди використовуй IsVisibleTo() — " +
        "він не залежить від цього поля."
    )]
    public BatalionManagerScr playerManager;

    // =========================================================
    // MAP
    // =========================================================

    [Header("Fog Grid")]

    [Tooltip("Центр області, яку покриває Fog of War.")]
    public Vector2 mapCenter = Vector2.zero;

    [Tooltip("Розмір області Fog of War.")]
    public Vector2 mapSize = new Vector2(200f, 200f);

    [Tooltip("Розмір однієї комірки Fog.")]
    [Min(0.5f)]
    public float cellSize = 2f;

    // =========================================================
    // VISUAL
    // =========================================================

    [Header("Visual")]

    public Material fogMaterial;

    [Tooltip("Колір повністю невідомої території.")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 0.95f);

    [Tooltip("Колір дослідженої, але зараз невидимої території.")]
    public Color exploredColor = new Color(0f, 0f, 0f, 0.55f);

    public string sortingLayerName = "Default";

    public int sortingOrder = 100;

    // =========================================================
    // UPDATE
    // =========================================================

    [Header("Update")]

    [Min(0.02f)]
    public float updateInterval = 0.15f;

    // =========================================================
    // INTERNAL — RENDER GRID (тільки playerManager)
    // =========================================================

    private float visibilityTimer;

    /// <summary>
    /// Дружні до playerManager команди. Публічне API
    /// (IsFriendlyTeam/IsEnemyTeam/IsNeutralTeam) і надалі описує
    /// САМЕ точку зору playerManager — для сумісності з рештою
    /// коду (напр. BatalionManagerScr.IsPlayerFriendly()).
    /// </summary>
    private readonly HashSet<int> friendlyTeamIDs = new HashSet<int>();

    private readonly List<BattalionScr> spottersBuffer = new List<BattalionScr>();

    private FogCell[,] cells;

    private int gridWidth;

    private int gridHeight;

    private Transform fogContainer;

    private Sprite fogSprite;

    private class FogCell
    {
        public Vector2 worldPosition;
        public bool explored;
        public bool visible;
        public SpriteRenderer renderer;
    }

    // =========================================================
    // INTERNAL — VISION PER TEAM (для IsVisibleTo, будь-яка команда)
    // =========================================================

    private class TeamVisionState
    {
        public HashSet<int> friendlyTeamIDs = new HashSet<int>();
        public bool[,] visibleCells;
    }

    /// <summary>
    /// Кеш стану видимості кожної "коаліції" (кореневий teamID
    /// менеджера -> що ця команда+союзники бачать). Перебудовується
    /// щоразу в RecalculateVisibility(), тому завжди свіжий.
    /// </summary>
    private readonly Dictionary<int, TeamVisionState> visionByTeam =
        new Dictionary<int, TeamVisionState>();

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Знищуємо лише сам компонент, а не весь GameObject —
            // друга копія цього скрипта не повинна забирати з собою
            // все інше, що висить на тому самому об'єкті.
            Debug.LogError(
                $"FogOfWarManagerScr: знайдено другий екземпляр на " +
                $"об'єкті '{gameObject.name}' (перший — на " +
                $"'{Instance.gameObject.name}'). FogOfWarManagerScr " +
                $"має бути рівно один на сцену.",
                this
            );

            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        CreateFogGrid();
        RecalculateVisibility();
    }

    private void Update()
    {
        visibilityTimer -= Time.deltaTime;

        if (visibilityTimer > 0f)
            return;

        visibilityTimer = updateInterval;

        RecalculateVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // TEAM RELATIONS (playerManager — для рендеру й старого API)
    // =========================================================

    /// <summary>
    /// Оновлює friendlyTeamIDs для playerManager. Шукає союз В
    /// ОБИДВА БОКИ (playerManager.IsAlly(x) АБО x.IsAlly(playerManager)),
    /// тож достатньо прописати allyID один раз — на будь-якому з
    /// двох менеджерів.
    /// </summary>
    public void RefreshFriendlyTeams()
    {
        friendlyTeamIDs.Clear();

        if (playerManager == null)
        {
            Debug.LogWarning("FogOfWarManagerScr: playerManager не призначений.");
            return;
        }

        friendlyTeamIDs.UnionWith(ComputeFriendlyTeamIDs(playerManager));
    }

    /// <summary>
    /// Дружні команди для довільного менеджера "forManager" —
    /// той самий білатеральний пошук союзу, що й RefreshFriendlyTeams,
    /// але для БУДЬ-ЯКОГО менеджера, не тільки playerManager.
    /// Явно оголошений ворог (в будь-яку сторону) має пріоритет
    /// над союзом.
    /// </summary>
    private HashSet<int> ComputeFriendlyTeamIDs(BatalionManagerScr forManager)
    {
        HashSet<int> result = new HashSet<int>();

        if (forManager == null)
            return result;

        result.Add(forManager.teamID);

        BatalionManagerScr[] allManagers = FindObjectsOfType<BatalionManagerScr>();

        for (int i = 0; i < allManagers.Length; i++)
        {
            BatalionManagerScr manager = allManagers[i];

            if (manager == null || manager == forManager)
                continue;

            int otherTeamID = manager.teamID;

            bool declaredEnemy =
                forManager.IsEnemy(otherTeamID) ||
                manager.IsEnemy(forManager.teamID);

            if (declaredEnemy)
                continue;

            bool declaredAlly =
                forManager.IsAlly(otherTeamID) ||
                manager.IsAlly(forManager.teamID);

            if (declaredAlly)
                result.Add(otherTeamID);
        }

        return result;
    }

    /// <summary>Чи є команда дружньою для playerManager.</summary>
    public bool IsFriendlyTeam(int teamID)
    {
        return friendlyTeamIDs.Contains(teamID);
    }

    /// <summary>Чи є команда ворогом playerManager. На туман не впливає.</summary>
    public bool IsEnemyTeam(int teamID)
    {
        if (playerManager == null)
            return false;

        return playerManager.IsEnemy(teamID);
    }

    /// <summary>Нейтральна щодо playerManager: не дружня і не ворожа.</summary>
    public bool IsNeutralTeam(int teamID)
    {
        if (IsFriendlyTeam(teamID))
            return false;

        if (IsEnemyTeam(teamID))
            return false;

        return true;
    }

    // =========================================================
    // PUBLIC: ЗАПИТ ВИДИМОСТІ З ТОЧКИ ЗОРУ БУДЬ-ЯКОГО МЕНЕДЖЕРА
    // =========================================================

    /// <summary>
    /// Чи бачить команда "viewer" батальйон "target" — НЕЗАЛЕЖНО
    /// від того, що зараз намальовано на екрані (playerManager).
    ///
    /// Саме цей метод варто використовувати в ігровій логіці (AI
    /// ворога/союзника: чи можу я вибрати цю ціль, чи бачу я її).
    /// Дає коректний результат навіть якщо viewer — не playerManager:
    /// той самий батальйон може одночасно бути visible=true для
    /// однієї команди і visible=false для іншої.
    /// </summary>
    public bool IsVisibleTo(BatalionManagerScr viewer, BattalionScr target)
    {
        if (viewer == null || target == null)
            return false;

        if (!visionByTeam.TryGetValue(viewer.teamID, out TeamVisionState state))
            return false;

        if (state.friendlyTeamIDs.Contains(target.teamID))
            return true;

        return IsPositionVisibleInState(state, target.transform.position);
    }

    /// <summary>Перевантаження за teamID замість посилання на менеджер.</summary>
    public bool IsVisibleTo(int viewerTeamID, BattalionScr target)
    {
        if (target == null)
            return false;

        if (!visionByTeam.TryGetValue(viewerTeamID, out TeamVisionState state))
            return false;

        if (state.friendlyTeamIDs.Contains(target.teamID))
            return true;

        return IsPositionVisibleInState(state, target.transform.position);
    }

    private bool IsPositionVisibleInState(TeamVisionState state, Vector2 position)
    {
        if (state.visibleCells == null || cells == null)
            return false;

        if (!TryGetCellIndex(position, out int x, out int y))
            return false;

        return state.visibleCells[x, y];
    }

    private bool TryGetCellIndex(Vector2 position, out int x, out int y)
    {
        Vector2 bottomLeft = mapCenter - mapSize * 0.5f;
        Vector2 localPosition = position - bottomLeft;

        x = Mathf.FloorToInt(localPosition.x / cellSize);
        y = Mathf.FloorToInt(localPosition.y / cellSize);

        return x >= 0 && y >= 0 && x < gridWidth && y < gridHeight;
    }

    // =========================================================
    // GRID CREATION (геометрія — спільна для всіх команд)
    // =========================================================

    private void CreateFogGrid()
    {
        ClearFogGrid();

        gridWidth = Mathf.CeilToInt(mapSize.x / cellSize);
        gridHeight = Mathf.CeilToInt(mapSize.y / cellSize);

        cells = new FogCell[gridWidth, gridHeight];

        GameObject containerObject = new GameObject("FogOfWarGrid");
        containerObject.transform.SetParent(transform);
        containerObject.transform.localPosition = Vector3.zero;
        fogContainer = containerObject.transform;

        CreateFogSprite();

        Vector2 bottomLeft = mapCenter - mapSize * 0.5f;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2 position = bottomLeft + new Vector2(
                    (x + 0.5f) * cellSize,
                    (y + 0.5f) * cellSize
                );

                FogCell cell = new FogCell();
                cell.worldPosition = position;

                GameObject cellObject = new GameObject($"Fog_{x}_{y}");
                cellObject.transform.SetParent(fogContainer);
                cellObject.transform.position = new Vector3(position.x, position.y, 0f);

                SpriteRenderer renderer = cellObject.AddComponent<SpriteRenderer>();
                renderer.sprite = fogSprite;
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder;

                if (fogMaterial != null)
                    renderer.material = fogMaterial;

                cellObject.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                renderer.color = unexploredColor;

                cell.renderer = renderer;
                cell.explored = false;
                cell.visible = false;

                cells[x, y] = cell;
            }
        }
    }

    private void ClearFogGrid()
    {
        if (fogContainer != null)
        {
            if (Application.isPlaying)
                Destroy(fogContainer.gameObject);
            else
                DestroyImmediate(fogContainer.gameObject);
        }

        fogContainer = null;
        cells = null;
    }

    private void CreateFogSprite()
    {
        if (fogSprite != null)
            return;

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fogSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
    }

    // =========================================================
    // VISIBILITY — ГОЛОВНИЙ ПЕРЕРАХУНОК
    // =========================================================

    public void RecalculateVisibility()
    {
        if (cells == null)
            return;

        RefreshFriendlyTeams();

        RebuildVisionStates();

        ApplyRenderStateFromPlayer();

        UpdateFogVisuals();

        RecalculateBattalionVisibility();
    }

    /// <summary>
    /// Перераховує TeamVisionState для КОЖНОЇ команди, що зараз
    /// представлена BatalionManagerScr у сцені (гравець, союзники,
    /// вороги, нейтрали — усі отримують власний, повністю незалежний
    /// стан видимості).
    /// </summary>
    private void RebuildVisionStates()
    {
        visionByTeam.Clear();

        BatalionManagerScr[] allManagers = FindObjectsOfType<BatalionManagerScr>();
        IReadOnlyList<BattalionScr> allBattalions = BattalionScr.AllActive;

        for (int m = 0; m < allManagers.Length; m++)
        {
            BatalionManagerScr manager = allManagers[m];

            if (manager == null)
                continue;

            // Одна коаліція (кореневий teamID) рахується лише раз,
            // навіть якщо в сцені кілька менеджерів з тим самим teamID.
            if (visionByTeam.ContainsKey(manager.teamID))
                continue;

            TeamVisionState state = new TeamVisionState
            {
                friendlyTeamIDs = ComputeFriendlyTeamIDs(manager),
                visibleCells = new bool[gridWidth, gridHeight]
            };

            spottersBuffer.Clear();

            for (int i = 0; i < allBattalions.Count; i++)
            {
                BattalionScr battalion = allBattalions[i];

                if (battalion != null && state.friendlyTeamIDs.Contains(battalion.teamID))
                    spottersBuffer.Add(battalion);
            }

            RevealVisibleCellsInto(state.visibleCells, spottersBuffer);

            visionByTeam[manager.teamID] = state;
        }
    }

    private void RevealVisibleCellsInto(bool[,] grid, List<BattalionScr> spotters)
    {
        for (int i = 0; i < spotters.Count; i++)
        {
            BattalionScr spotter = spotters[i];

            if (spotter == null)
                continue;

            Vector2 origin = spotter.transform.position;

            float visionRange = spotter.GetEffectiveVisionRange();
            float visionRangeSquared = visionRange * visionRange;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (grid[x, y])
                        continue;

                    Vector2 cellPos = cells[x, y].worldPosition;
                    Vector2 offset = cellPos - origin;

                    if (offset.sqrMagnitude > visionRangeSquared)
                        continue;

                    if (TerrainManagerScr.Instance != null &&
                        !TerrainManagerScr.Instance.HasLineOfSight(origin, cellPos))
                    {
                        continue;
                    }

                    grid[x, y] = true;
                }
            }
        }
    }

    /// <summary>
    /// Копіює TeamVisionState гравця (playerManager) у cells[,] —
    /// саме це фактично малюється на екрані.
    /// </summary>
    private void ApplyRenderStateFromPlayer()
    {
        if (playerManager == null)
            return;

        if (!visionByTeam.TryGetValue(playerManager.teamID, out TeamVisionState state))
            return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                bool visible = state.visibleCells[x, y];

                cells[x, y].visible = visible;

                if (visible)
                    cells[x, y].explored = true;
            }
        }
    }

    // =========================================================
    // UPDATE FOG VISUALS (сіра заливка на екрані)
    // =========================================================

    private void UpdateFogVisuals()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                FogCell cell = cells[x, y];

                if (cell.renderer == null)
                    continue;

                if (cell.visible)
                {
                    cell.renderer.enabled = false;
                    continue;
                }

                cell.renderer.enabled = true;

                cell.renderer.color = cell.explored ? exploredColor : unexploredColor;
            }
        }
    }

    // =========================================================
    // BATTALION VISIBILITY (спрайт на екрані — теж лише playerManager)
    // =========================================================

    /// <summary>
    /// Ховає/показує спрайт батальйону на екрані. Це рендер, тому
    /// рахується виключно з точки зору playerManager — так само,
    /// як і сіра заливка. Для запиту з точки зору ІНШОЇ команди
    /// використовуй IsVisibleTo(), він на battalion.SetFogVisible
    /// не впливає і від нього не залежить.
    /// </summary>
    private void RecalculateBattalionVisibility()
    {
        if (playerManager == null)
            return;

        IReadOnlyList<BattalionScr> all = BattalionScr.AllActive;

        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion = all[i];

            if (battalion == null)
                continue;

            bool visible = IsVisibleTo(playerManager, battalion);

            battalion.SetFogVisible(visible);
        }
    }

    // =========================================================
    // POSITION VISIBILITY (сумісність зі старим API — точка зору playerManager)
    // =========================================================

    public bool IsWorldPositionVisible(Vector2 position)
    {
        if (cells == null)
            return false;

        if (!TryGetCellIndex(position, out int x, out int y))
            return false;

        return cells[x, y].visible;
    }

    // =========================================================
    // PUBLIC REBUILD
    // =========================================================

    [ContextMenu("Rebuild Fog Grid")]
    public void RebuildFogGrid()
    {
        CreateFogGrid();
        RecalculateVisibility();
    }

    [ContextMenu("Refresh Team Relations")]
    public void RefreshTeamRelations()
    {
        RecalculateVisibility();
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireCube(mapCenter, mapSize);
    }
}
