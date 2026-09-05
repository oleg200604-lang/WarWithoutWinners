using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fog of War.
///
/// Основний принцип:
///
/// 1. Геометрія Fog Grid створюється один раз.
/// 2. Видимість команди зберігається в TeamVisionState.
/// 3. Видимість конкретного батальйона зберігається окремо.
/// 4. Якщо батальйон не змінив позицію — його LOS не перераховується.
/// 5. Update() НЕ перераховує Fog. Він лише перевіряє, чи змінився стан.
/// 6. Логічна видимість і візуальний Fog повністю розділені.
/// 7. playerManager впливає тільки на те, що бачить гравець на екрані.
///
/// Public API старої системи збережено максимально можливо.
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
        "на екрані. Логічні запити інших команд використовують " +
        "IsVisibleTo()."
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
    public Color unexploredColor =
        new Color(0f, 0f, 0f, 0.95f);

    [Tooltip("Колір дослідженої, але зараз невидимої території.")]
    public Color exploredColor =
        new Color(0f, 0f, 0f, 0.55f);

    public string sortingLayerName = "Default";

    public int sortingOrder = 100;

    // =========================================================
    // UPDATE
    // =========================================================

    [Header("Update")]

    [Min(0.02f)]
    public float updateInterval = 0.15f;

    // =========================================================
    // INTERNAL — RENDER GRID
    // =========================================================

    private float visibilityTimer;

    private FogCell[,] cells;

    private int gridWidth;
    private int gridHeight;

    private Transform fogContainer;

    private Sprite fogSprite;

    /// <summary>
    /// Команди, дружні до playerManager.
    /// Зберігається для сумісності зі старим API.
    /// </summary>
    private readonly HashSet<int> friendlyTeamIDs =
        new HashSet<int>();

    private class FogCell
    {
        public Vector2 worldPosition;

        public bool explored;
        public bool visible;

        public SpriteRenderer renderer;
    }

    // =========================================================
    // INTERNAL — TEAM VISION
    // =========================================================

    /// <summary>
    /// Стан видимості однієї команди.
    ///
    /// visibilityCount:
    /// кількість дружніх spotter'ів, які бачать конкретну клітинку.
    ///
    /// Це дозволяє правильно видаляти видимість, коли один spotter
    /// переміщується:
    ///
    /// count 3 -> 2
    /// клітинка все ще visible.
    ///
    /// count 1 -> 0
    /// клітинка стає invisible.
    /// </summary>
    private class TeamVisionState
    {
        public readonly HashSet<int> friendlyTeamIDs =
            new HashSet<int>();

        public int[] visibilityCount;

        public readonly HashSet<int> visibleCellIndices =
            new HashSet<int>();
    }

    /// <summary>
    /// Видимість одного конкретного батальйона.
    ///
    /// Зберігається список клітинок, які бачить цей spotter.
    /// </summary>
    private class SpotterVision
    {
        public int teamID;

        public Vector2 position;

        public float visionRange;

        public readonly List<int> visibleCells =
            new List<int>();
    }

    /// <summary>
    /// TeamID -> стан видимості.
    /// </summary>
    private readonly Dictionary<int, TeamVisionState> visionByTeam =
        new Dictionary<int, TeamVisionState>();

    /// <summary>
    /// Battalion -> його кешована зона видимості.
    /// </summary>
    private readonly Dictionary<BattalionScr, SpotterVision> spotterVision =
        new Dictionary<BattalionScr, SpotterVision>();

    // =========================================================
    // INTERNAL — MANAGERS
    // =========================================================

    /// <summary>
    /// Менеджери кешуються.
    ///
    /// FindObjectsOfType НЕ використовується всередині основного
    /// visibility loop.
    /// </summary>
    private readonly List<BatalionManagerScr> managers =
        new List<BatalionManagerScr>();

    // =========================================================
    // INTERNAL — STATE TRACKING
    // =========================================================

    private class BattalionSnapshot
    {
        public Vector2 position;
        public int teamID;
        public float baseVisionRange;
    }

    private readonly Dictionary<BattalionScr, BattalionSnapshot>
        battalionSnapshots =
        new Dictionary<BattalionScr, BattalionSnapshot>();

    private bool visibilityDirty = true;

    private bool initialized;

    private bool relationsDirty = true;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                $"FogOfWarManagerScr: знайдено другий екземпляр на " +
                $"об'єкті '{gameObject.name}'. Перший екземпляр знаходиться " +
                $"на '{Instance.gameObject.name}'. Повинен існувати лише " +
                $"один FogOfWarManagerScr.",
                this
            );

            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        RefreshManagerCache();

        CreateFogGrid();

        initialized = true;

        visibilityDirty = true;

        RecalculateVisibility();
    }

    private void Update()
    {
        if (!initialized)
            return;

        visibilityTimer -= Time.deltaTime;

        if (visibilityTimer > 0f)
            return;

        visibilityTimer = Mathf.Max(0.02f, updateInterval);

        /*
         * ВАЖЛИВО:
         *
         * Тут більше немає RecalculateVisibility().
         *
         * Update лише дешево перевіряє, чи змінився стан батальйонів.
         */
        DetectBattalionChanges();

        if (visibilityDirty)
            RecalculateVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // MANAGER CACHE
    // =========================================================

    /// <summary>
    /// Оновлює кеш BatalionManagerScr.
    ///
    /// Це викликається при старті та при явному
    /// RefreshTeamRelations(), а не кожні 0.15 секунди.
    /// </summary>
    private void RefreshManagerCache()
    {
        managers.Clear();

        BatalionManagerScr[] found =
            FindObjectsOfType<BatalionManagerScr>();

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null)
                continue;

            if (!managers.Contains(found[i]))
                managers.Add(found[i]);
        }
    }

    // =========================================================
    // TEAM RELATIONS
    // =========================================================

    /// <summary>
    /// Оновлює дружні команди playerManager.
    ///
    /// Для сумісності з попередньою версією:
    /// союз вважається двостороннім.
    /// Ворожнеча має пріоритет над союзом.
    /// </summary>
    public void RefreshFriendlyTeams()
    {
        RefreshManagerCache();

        friendlyTeamIDs.Clear();

        if (playerManager == null)
        {
            Debug.LogWarning(
                "FogOfWarManagerScr: playerManager не призначений."
            );

            relationsDirty = true;
            visibilityDirty = true;
            return;
        }

        HashSet<int> result =
            ComputeFriendlyTeamIDs(playerManager);

        friendlyTeamIDs.UnionWith(result);

        relationsDirty = true;
        visibilityDirty = true;
    }

    /// <summary>
    /// Визначає всі команди, дружні до конкретного менеджера.
    ///
    /// Явний enemy з будь-якого боку має пріоритет над ally.
    /// </summary>
    private HashSet<int> ComputeFriendlyTeamIDs(
        BatalionManagerScr forManager)
    {
        HashSet<int> result =
            new HashSet<int>();

        if (forManager == null)
            return result;

        result.Add(forManager.teamID);

        for (int i = 0; i < managers.Count; i++)
        {
            BatalionManagerScr manager = managers[i];

            if (manager == null ||
                manager == forManager)
            {
                continue;
            }

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

    /// <summary>
    /// Чи є команда дружньою для playerManager.
    /// </summary>
    public bool IsFriendlyTeam(int teamID)
    {
        return friendlyTeamIDs.Contains(teamID);
    }

    /// <summary>
    /// Чи є команда ворогом playerManager.
    /// </summary>
    public bool IsEnemyTeam(int teamID)
    {
        if (playerManager == null)
            return false;

        return playerManager.IsEnemy(teamID);
    }

    /// <summary>
    /// Нейтральна щодо playerManager.
    /// </summary>
    public bool IsNeutralTeam(int teamID)
    {
        if (IsFriendlyTeam(teamID))
            return false;

        if (IsEnemyTeam(teamID))
            return false;

        return true;
    }

    // =========================================================
    // PUBLIC VISIBILITY API
    // =========================================================

    /// <summary>
    /// Чи бачить команда viewer батальйон target.
    ///
    /// Не залежить від playerManager.
    /// </summary>
    public bool IsVisibleTo(
        BatalionManagerScr viewer,
        BattalionScr target)
    {
        if (viewer == null ||
            target == null)
        {
            return false;
        }

        EnsureVisibilityUpToDate();

        if (!visionByTeam.TryGetValue(
                viewer.teamID,
                out TeamVisionState state))
        {
            /*
             * Власні/дружні батальйони завжди повинні бути видимими,
             * навіть якщо vision state ще не створено.
             */
            return viewer.IsAlly(target.teamID);
        }

        if (state.friendlyTeamIDs.Contains(target.teamID))
            return true;

        return IsPositionVisibleInState(
            state,
            target.transform.position
        );
    }

    /// <summary>
    /// Перевантаження за teamID.
    /// </summary>
    public bool IsVisibleTo(
        int viewerTeamID,
        BattalionScr target)
    {
        if (target == null)
            return false;

        EnsureVisibilityUpToDate();

        if (!visionByTeam.TryGetValue(
                viewerTeamID,
                out TeamVisionState state))
        {
            return false;
        }

        if (state.friendlyTeamIDs.Contains(target.teamID))
            return true;

        return IsPositionVisibleInState(
            state,
            target.transform.position
        );
    }

    /// <summary>
    /// Чи видима позиція з точки зору playerManager.
    /// </summary>
    public bool IsWorldPositionVisible(Vector2 position)
    {
        EnsureVisibilityUpToDate();

        if (cells == null)
            return false;

        if (!TryGetCellIndex(
                position,
                out int x,
                out int y))
        {
            return false;
        }

        return cells[x, y].visible;
    }

    // =========================================================
    // VISIBILITY STATE
    // =========================================================

    private bool IsPositionVisibleInState(
        TeamVisionState state,
        Vector2 position)
    {
        if (state == null ||
            state.visibilityCount == null ||
            cells == null)
        {
            return false;
        }

        if (!TryGetCellIndex(
                position,
                out int x,
                out int y))
        {
            return false;
        }

        int index =
            GetCellIndex(x, y);

        return state.visibilityCount[index] > 0;
    }

    // =========================================================
    // CHANGE DETECTION
    // =========================================================

    /// <summary>
    /// Перевіряє, чи змінилась позиція/команда батальйонів.
    ///
    /// Цей метод навмисно не робить жодних Physics2D запитів.
    /// </summary>
    private void DetectBattalionChanges()
    {
        IReadOnlyList<BattalionScr> all =
            BattalionScr.AllActive;

        bool structureChanged =
            all.Count != battalionSnapshots.Count;

        if (structureChanged)
        {
            visibilityDirty = true;
            return;
        }

        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion = all[i];

            if (battalion == null)
            {
                visibilityDirty = true;
                return;
            }

            if (!battalionSnapshots.TryGetValue(
                    battalion,
                    out BattalionSnapshot snapshot))
            {
                visibilityDirty = true;
                return;
            }

            Vector2 position =
                battalion.transform.position;

            int teamID =
                battalion.teamID;

            float baseVisionRange =
                GetBaseVisionRange(battalion);

            if (snapshot.teamID != teamID ||
                snapshot.position != position ||
                !Mathf.Approximately(
                    snapshot.baseVisionRange,
                    baseVisionRange))
            {
                visibilityDirty = true;
                return;
            }
        }
    }

    private float GetBaseVisionRange(
        BattalionScr battalion)
    {
        if (battalion == null ||
            battalion.battalion == null)
        {
            return 0f;
        }

        return battalion.battalion.visionRange;
    }

    /// <summary>
    /// Перевіряє стан безпосередньо перед логічним запитом.
    ///
    /// Це захищає систему від ситуації, коли батальйон перемістився
    /// між двома тиками Update(), а AI вже намагається перевірити
    /// його видимість.
    /// </summary>
    private void EnsureVisibilityUpToDate()
    {
        if (!initialized)
            return;

        DetectBattalionChanges();

        if (visibilityDirty)
            RecalculateVisibility();
    }

    /// <summary>
    /// Примусово позначає Fog як такий, що потребує перерахунку.
    ///
    /// Можна викликати з інших систем після зміни terrain,
    /// відносин або параметрів vision.
    /// </summary>
    public void MarkVisibilityDirty()
    {
        visibilityDirty = true;
    }

    // =========================================================
    // MAIN VISIBILITY CALCULATION
    // =========================================================

    /// <summary>
    /// Повністю перебудовує логічну видимість.
    ///
    /// ВАЖЛИВО:
    /// Цей метод більше НЕ викликається кожні 0.15 секунди.
    ///
    /// Повна перебудова потрібна тільки після:
    /// - зміни дипломатичних відносин;
    /// - появи/зникнення батальйонів;
    /// - першого запуску;
    /// - інших глобальних змін.
    ///
    /// Звичайний рух батальйона обробляється через кеш spotter'а.
    /// </summary>
    public void RecalculateVisibility()
    {
        if (!initialized &&
            cells == null)
        {
            return;
        }

        if (cells == null)
            return;

        if (relationsDirty)
        {
            RefreshManagerCache();
            RebuildVisionStates();
            relationsDirty = false;

            /*
             * RebuildVisionStates вже повністю порахував видимість.
             * Після нього просто оновлюємо snapshots і render.
             */
            UpdateSnapshots();

            ApplyRenderStateFromPlayer();
            RecalculateBattalionVisibility();

            visibilityDirty = false;

            return;
        }

        /*
         * Якщо дипломатія не змінилась — не треба будувати
         * всю систему з нуля.
         *
         * Оновлюємо тільки ті spotter'и, які змінилися.
         */
        UpdateChangedSpotters();

        UpdateSnapshots();

        ApplyRenderStateFromPlayer();
        RecalculateBattalionVisibility();

        visibilityDirty = false;
    }

    // =========================================================
    // FULL VISION BUILD
    // =========================================================

    private void RebuildVisionStates()
    {
        visionByTeam.Clear();
        spotterVision.Clear();

        friendlyTeamIDs.Clear();

        if (playerManager != null)
        {
            friendlyTeamIDs.UnionWith(
                ComputeFriendlyTeamIDs(playerManager)
            );
        }

        /*
         * Створюємо state для кожного унікального teamID.
         */
        for (int i = 0; i < managers.Count; i++)
        {
            BatalionManagerScr manager =
                managers[i];

            if (manager == null)
                continue;

            int teamID =
                manager.teamID;

            if (visionByTeam.ContainsKey(teamID))
                continue;

            CreateTeamVisionState(manager);
        }

        /*
         * На випадок, якщо Battalion існує, але менеджер
         * з якихось причин ще не представлений у managers.
         */
        IReadOnlyList<BattalionScr> all =
            BattalionScr.AllActive;

        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion = all[i];

            if (battalion == null)
                continue;

            if (visionByTeam.ContainsKey(battalion.teamID))
                continue;

            /*
             * Для команди без manager неможливо визначити союзників.
             * Вона бачить лише саму себе.
             */
            TeamVisionState state =
                new TeamVisionState();

            state.friendlyTeamIDs.Add(
                battalion.teamID
            );

            state.visibilityCount =
                new int[gridWidth * gridHeight];

            visionByTeam[battalion.teamID] =
                state;
        }

        /*
         * Тепер додаємо всіх spotter'ів у відповідні states.
         */
        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion = all[i];

            if (battalion == null)
                continue;

            AddSpotterToAllRelevantTeams(
                battalion
            );
        }
    }

    private void CreateTeamVisionState(
        BatalionManagerScr manager)
    {
        TeamVisionState state =
            new TeamVisionState();

        state.visibilityCount =
            new int[gridWidth * gridHeight];

        HashSet<int> friendly =
            ComputeFriendlyTeamIDs(manager);

        state.friendlyTeamIDs.UnionWith(
            friendly
        );

        visionByTeam[manager.teamID] =
            state;
    }

    // =========================================================
    // SPOTTER CACHE
    // =========================================================

    /// <summary>
    /// Додає spotter у всі TeamVisionState, для яких його teamID
    /// є дружнім.
    /// </summary>
    private void AddSpotterToAllRelevantTeams(
        BattalionScr battalion)
    {
        if (battalion == null)
            return;

        SpotterVision vision =
            BuildSpotterVision(battalion);

        spotterVision[battalion] =
            vision;

        foreach (KeyValuePair<int, TeamVisionState> pair
                 in visionByTeam)
        {
            TeamVisionState state =
                pair.Value;

            if (!state.friendlyTeamIDs.Contains(
                    battalion.teamID))
            {
                continue;
            }

            AddSpotterVisionToState(
                state,
                vision
            );
        }
    }

    /// <summary>
    /// Будує видимі клітинки одного батальйона.
    ///
    /// КРИТИЧНА оптимізація:
    ///
    /// Старий код:
    ///
    /// battalion
    ///     -> кожна клітинка всієї карти
    ///
    /// Новий код:
    ///
    /// battalion
    ///     -> тільки прямокутник навколо vision range
    ///     -> тільки клітинки всередині кола
    ///     -> LOS тільки для них.
    /// </summary>
    private SpotterVision BuildSpotterVision(
        BattalionScr battalion)
    {
        SpotterVision result =
            new SpotterVision();

        result.teamID =
            battalion.teamID;

        result.position =
            battalion.transform.position;

        result.visionRange =
            battalion.GetEffectiveVisionRange();

        if (result.visionRange <= 0f)
            return result;

        Vector2 position =
            result.position;

        float range =
            result.visionRange;

        Vector2 minPosition =
            position - Vector2.one * range;

        Vector2 maxPosition =
            position + Vector2.one * range;

        if (!TryGetCellIndex(
                minPosition,
                out int minX,
                out int minY))
        {
            /*
             * Позиція може бути за межами Fog Grid.
             * Тут просто обмежуємо вручну.
             */
            Vector2 bottomLeft =
                mapCenter - mapSize * 0.5f;

            minX = Mathf.FloorToInt(
                (minPosition.x - bottomLeft.x) /
                cellSize
            );

            minY = Mathf.FloorToInt(
                (minPosition.y - bottomLeft.y) /
                cellSize
            );
        }

        if (!TryGetCellIndex(
                maxPosition,
                out int maxX,
                out int maxY))
        {
            Vector2 bottomLeft =
                mapCenter - mapSize * 0.5f;

            maxX = Mathf.FloorToInt(
                (maxPosition.x - bottomLeft.x) /
                cellSize
            );

            maxY = Mathf.FloorToInt(
                (maxPosition.y - bottomLeft.y) /
                cellSize
            );
        }

        minX = Mathf.Clamp(
            minX,
            0,
            gridWidth - 1
        );

        minY = Mathf.Clamp(
            minY,
            0,
            gridHeight - 1
        );

        maxX = Mathf.Clamp(
            maxX,
            0,
            gridWidth - 1
        );

        maxY = Mathf.Clamp(
            maxY,
            0,
            gridHeight - 1
        );

        float rangeSquared =
            range * range;

        TerrainManagerScr terrain =
            TerrainManagerScr.Instance;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                FogCell cell =
                    cells[x, y];

                Vector2 offset =
                    cell.worldPosition - position;

                if (offset.sqrMagnitude >
                    rangeSquared)
                {
                    continue;
                }

                /*
                 * Власна клітинка очевидно видима.
                 * Це також економить зайвий Physics2D raycast.
                 */
                if (offset.sqrMagnitude <= 0.0001f)
                {
                    result.visibleCells.Add(
                        GetCellIndex(x, y)
                    );

                    continue;
                }

                /*
                 * LOS — єдиний дорогий фізичний запит.
                 * Але він тепер робиться тільки для клітинок
                 * всередині реального радіуса бачення.
                 */
                if (terrain != null &&
                    !terrain.HasLineOfSight(
                        position,
                        cell.worldPosition))
                {
                    continue;
                }

                result.visibleCells.Add(
                    GetCellIndex(x, y)
                );
            }
        }

        return result;
    }

    // =========================================================
    // INCREMENTAL SPOTTER UPDATE
    // =========================================================

    /// <summary>
    /// Оновлює тільки spotter'и, стан яких змінився.
    ///
    /// Це головна відмінність від старої системи.
    /// </summary>
    private void UpdateChangedSpotters()
    {
        IReadOnlyList<BattalionScr> all =
            BattalionScr.AllActive;

        /*
         * Спочатку видаляємо spotter'и, яких більше немає.
         */
        List<BattalionScr> removed =
            null;

        foreach (KeyValuePair<BattalionScr, SpotterVision> pair
                 in spotterVision)
        {
            BattalionScr battalion =
                pair.Key;

            if (battalion == null ||
                !ContainsBattalion(all, battalion))
            {
                if (removed == null)
                    removed = new List<BattalionScr>();

                removed.Add(battalion);
            }
        }

        if (removed != null)
        {
            for (int i = 0; i < removed.Count; i++)
            {
                BattalionScr battalion =
                    removed[i];

                if (battalion != null)
                    RemoveSpotterFromAllTeams(
                        battalion
                    );

                spotterVision.Remove(
                    battalion
                );
            }
        }

        /*
         * Нові або переміщені battalion'и.
         */
        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion =
                all[i];

            if (battalion == null)
                continue;

            bool needsUpdate =
                !spotterVision.ContainsKey(battalion);

            if (!needsUpdate)
            {
                SpotterVision oldVision =
                    spotterVision[battalion];

                Vector2 currentPosition =
                    battalion.transform.position;

                float currentRange =
                    battalion.GetEffectiveVisionRange();

                if (oldVision.teamID != battalion.teamID ||
                    oldVision.position != currentPosition ||
                    !Mathf.Approximately(
                        oldVision.visionRange,
                        currentRange))
                {
                    needsUpdate = true;
                }
            }

            if (!needsUpdate)
                continue;

            /*
             * Якщо старий spotter існує — спочатку прибираємо
             * його стару видимість.
             */
            if (spotterVision.ContainsKey(battalion))
            {
                RemoveSpotterFromAllTeams(
                    battalion
                );
            }

            AddSpotterToAllRelevantTeams(
                battalion
            );
        }
    }

    private bool ContainsBattalion(
        IReadOnlyList<BattalionScr> list,
        BattalionScr target)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == target)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Видаляє стару зону бачення батальйона з усіх команд.
    /// </summary>
    private void RemoveSpotterFromAllTeams(
        BattalionScr battalion)
    {
        if (battalion == null)
            return;

        if (!spotterVision.TryGetValue(
                battalion,
                out SpotterVision vision))
        {
            return;
        }

        foreach (KeyValuePair<int, TeamVisionState> pair
                 in visionByTeam)
        {
            TeamVisionState state =
                pair.Value;

            if (!state.friendlyTeamIDs.Contains(
                    vision.teamID))
            {
                continue;
            }

            RemoveSpotterVisionFromState(
                state,
                vision
            );
        }
    }

    // =========================================================
    // TEAM VISION COUNT
    // =========================================================

    private void AddSpotterVisionToState(
        TeamVisionState state,
        SpotterVision vision)
    {
        if (state == null ||
            vision == null ||
            state.visibilityCount == null)
        {
            return;
        }

        for (int i = 0;
             i < vision.visibleCells.Count;
             i++)
        {
            int index =
                vision.visibleCells[i];

            if (index < 0 ||
                index >= state.visibilityCount.Length)
            {
                continue;
            }

            state.visibilityCount[index]++;

            state.visibleCellIndices.Add(
                index
            );
        }
    }

    private void RemoveSpotterVisionFromState(
        TeamVisionState state,
        SpotterVision vision)
    {
        if (state == null ||
            vision == null ||
            state.visibilityCount == null)
        {
            return;
        }

        for (int i = 0;
             i < vision.visibleCells.Count;
             i++)
        {
            int index =
                vision.visibleCells[i];

            if (index < 0 ||
                index >= state.visibilityCount.Length)
            {
                continue;
            }

            state.visibilityCount[index]--;

            if (state.visibilityCount[index] <= 0)
            {
                state.visibilityCount[index] = 0;

                state.visibleCellIndices.Remove(
                    index
                );
            }
        }
    }

    // =========================================================
    // SNAPSHOTS
    // =========================================================

    private void UpdateSnapshots()
    {
        battalionSnapshots.Clear();

        IReadOnlyList<BattalionScr> all =
            BattalionScr.AllActive;

        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion =
                all[i];

            if (battalion == null)
                continue;

            BattalionSnapshot snapshot =
                new BattalionSnapshot();

            snapshot.position =
                battalion.transform.position;

            snapshot.teamID =
                battalion.teamID;

            snapshot.baseVisionRange =
                GetBaseVisionRange(
                    battalion
                );

            battalionSnapshots[battalion] =
                snapshot;
        }
    }

    // =========================================================
    // RENDER STATE
    // =========================================================

    /// <summary>
    /// Переносить логічну видимість playerManager
    /// у render grid.
    ///
    /// Самі TeamVisionState при цьому НЕ змінюються.
    /// </summary>
    private void ApplyRenderStateFromPlayer()
    {
        if (playerManager == null)
        {
            /*
             * Якщо playerManager не призначений,
             * приховуємо все.
             */
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    SetCellVisible(
                        cells[x, y],
                        false
                    );
                }
            }

            return;
        }

        if (!visionByTeam.TryGetValue(
                playerManager.teamID,
                out TeamVisionState state))
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    SetCellVisible(
                        cells[x, y],
                        false
                    );
                }
            }

            return;
        }

        /*
         * Важливо:
         *
         * Тут ми все ще проходимо grid.
         * Але це тільки простий int/bool lookup.
         *
         * Немає:
         * - Physics2D
         * - пошуку батальйонів
         * - terrain queries
         * - LOS
         *
         * І renderer змінюється лише якщо стан реально змінився.
         */
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                int index =
                    GetCellIndex(x, y);

                bool visible =
                    state.visibilityCount[index] > 0;

                SetCellVisible(
                    cells[x, y],
                    visible
                );
            }
        }
    }

    private void SetCellVisible(
        FogCell cell,
        bool visible)
    {
        if (cell == null)
            return;

        bool wasVisible =
            cell.visible;

        cell.visible =
            visible;

        if (visible)
            cell.explored = true;

        if (cell.renderer == null)
            return;

        /*
         * Нічого не змінюємо, якщо стан не змінився.
         */
        if (wasVisible == visible)
        {
            /*
             * Але explored може змінитися з false -> true
             * під час першого відкриття.
             */
            if (!visible)
                return;
        }

        if (visible)
        {
            cell.renderer.enabled = false;
        }
        else
        {
            cell.renderer.enabled = true;

            cell.renderer.color =
                cell.explored
                    ? exploredColor
                    : unexploredColor;
        }
    }

    // =========================================================
    // BATTALION RENDER VISIBILITY
    // =========================================================

    /// <summary>
    /// Оновлює фізичне відображення батальйонів на екрані.
    ///
    /// Це виключно playerManager.
    /// Інші команди мають власні TeamVisionState.
    /// </summary>
    private void RecalculateBattalionVisibility()
    {
        if (playerManager == null)
            return;

        IReadOnlyList<BattalionScr> all =
            BattalionScr.AllActive;

        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion =
                all[i];

            if (battalion == null)
                continue;

            /*
             * Дружні завжди видимі.
             */
            bool visible;

            if (friendlyTeamIDs.Contains(
                    battalion.teamID))
            {
                visible = true;
            }
            else
            {
                visible =
                    IsPositionVisibleInPlayerState(
                        battalion.transform.position
                    );
            }

            battalion.SetFogVisible(
                visible
            );
        }
    }

    private bool IsPositionVisibleInPlayerState(
        Vector2 position)
    {
        if (playerManager == null)
            return false;

        if (!visionByTeam.TryGetValue(
                playerManager.teamID,
                out TeamVisionState state))
        {
            return false;
        }

        return IsPositionVisibleInState(
            state,
            position
        );
    }

    // =========================================================
    // GRID
    // =========================================================

    private bool TryGetCellIndex(
        Vector2 position,
        out int x,
        out int y)
    {
        Vector2 bottomLeft =
            mapCenter - mapSize * 0.5f;

        Vector2 localPosition =
            position - bottomLeft;

        x =
            Mathf.FloorToInt(
                localPosition.x / cellSize
            );

        y =
            Mathf.FloorToInt(
                localPosition.y / cellSize
            );

        return
            x >= 0 &&
            y >= 0 &&
            x < gridWidth &&
            y < gridHeight;
    }

    private int GetCellIndex(
        int x,
        int y)
    {
        return
            y * gridWidth + x;
    }

    // =========================================================
    // GRID CREATION
    // =========================================================

    private void CreateFogGrid()
    {
        ClearFogGrid();

        if (cellSize <= 0f)
            cellSize = 2f;

        gridWidth =
            Mathf.CeilToInt(
                mapSize.x / cellSize
            );

        gridHeight =
            Mathf.CeilToInt(
                mapSize.y / cellSize
            );

        gridWidth =
            Mathf.Max(1, gridWidth);

        gridHeight =
            Mathf.Max(1, gridHeight);

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
            mapCenter - mapSize * 0.5f;

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
                        (x + 0.5f) * cellSize,
                        (y + 0.5f) * cellSize
                    );

                FogCell cell =
                    new FogCell();

                cell.worldPosition =
                    position;

                cell.explored =
                    false;

                cell.visible =
                    false;

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
                    cellObject.AddComponent<SpriteRenderer>();

                renderer.sprite =
                    fogSprite;

                renderer.sortingLayerName =
                    sortingLayerName;

                renderer.sortingOrder =
                    sortingOrder;

                if (fogMaterial != null)
                    renderer.material =
                        fogMaterial;

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

        fogContainer = null;
        cells = null;

        visionByTeam.Clear();
        spotterVision.Clear();
        battalionSnapshots.Clear();
    }

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
    // PUBLIC REBUILD
    // =========================================================

    [ContextMenu("Rebuild Fog Grid")]
    public void RebuildFogGrid()
    {
        CreateFogGrid();

        relationsDirty = true;
        visibilityDirty = true;

        if (initialized)
            RecalculateVisibility();
    }

    [ContextMenu("Refresh Team Relations")]
    public void RefreshTeamRelations()
    {
        /*
         * Це тепер справді глобальна операція:
         * змінюються friendlyTeamIDs,
         * тому старі TeamVisionState більше не можна
         * використовувати.
         */
        RefreshManagerCache();

        relationsDirty = true;
        visibilityDirty = true;

        if (initialized)
            RecalculateVisibility();
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(
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