using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Туман війни.
///
/// Дружні команди беруться НЕ вручну, а напряму з BatalionManagerScr:
///  - playerManager.teamID — команда гравця;
///  - будь-який інший BatalionManagerScr у сцені з isAllyOfPlayer == true
///    (наприклад, БатальйонМенеджер союзного AI) додає свій teamID
///    у список дружніх автоматично.
///
/// Приховує батальйони, чий teamID НЕ входить у дружні, і затемнює
/// плитки шару Terrain (TerrainTileScr), які зараз не проглядаються
/// жодним дружнім батальйоном. Дружні батальйони й плитки, які вони
/// бачать, ніколи не приховуються.
///
/// Ворог/плитка вважаються видимими, якщо хоча б один дружній
/// батальйон знаходиться в межах його ефективної дальності
/// виявлення (GetEffectiveVisionRange — залежить від місцевості,
/// на якій стоїть спостерігач) І лінія зору не заблокована
/// місцевістю (ліс/гори).
///
/// Один екземпляр на сцену.
/// </summary>
public class FogOfWarManagerScr : MonoBehaviour
{
    public static FogOfWarManagerScr Instance { get; private set; }

    [Header("Інтеграція з BatalionManagerScr")]
    [Tooltip("BatalionManagerScr гравця. Його teamID автоматично вважається дружнім.")]
    public BatalionManagerScr playerManager;

    [Tooltip("Необов'язково: якщо не заповнено — усі BatalionManagerScr у сцені з isAllyOfPlayer == true знаходяться автоматично (FindObjectsOfType). Заповніть вручну лише якщо потрібно обмежити пошук конкретними менеджерами.")]
    public List<BatalionManagerScr> allyManagers = new List<BatalionManagerScr>();

    [Header("Оновлення")]
    [Tooltip("Як часто (у секундах) перераховувати видимість. 0 = щокадру.")]
    [Min(0f)]
    public float updateInterval = 0.2f;

    [Tooltip("Як часто (у секундах) оновлювати список дружніх teamID з BatalionManagerScr. Менеджери майже ніколи не змінюються під час гри, тому це можна робити рідше за саму видимість.")]
    [Min(0f)]
    public float friendlyTeamsRefreshInterval = 1f;

    private float visibilityTimer;
    private float friendlyTeamsTimer;

    private readonly HashSet<int> friendlyTeamIDs = new HashSet<int>();
    private readonly List<BattalionScr> spottersBuffer = new List<BattalionScr>();
    private readonly List<BattalionScr> targetsBuffer = new List<BattalionScr>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Рахуємо одразу на старті, щоб не було жодного кадру,
        // де вороги/плитки видно "за замовчуванням" до першого Update.
        RefreshFriendlyTeams();
        RecalculateVisibility();
    }

    private void Update()
    {
        friendlyTeamsTimer -= Time.deltaTime;

        if (friendlyTeamsTimer <= 0f)
        {
            friendlyTeamsTimer = friendlyTeamsRefreshInterval;
            RefreshFriendlyTeams();
        }

        visibilityTimer -= Time.deltaTime;

        if (visibilityTimer > 0f)
            return;

        visibilityTimer = updateInterval;

        RecalculateVisibility();
    }

    /// <summary>
    /// Перебудовує набір дружніх teamID з playerManager + союзних
    /// BatalionManagerScr (вручну заданих у allyManagers, або, якщо
    /// список порожній, знайдених автоматично по isAllyOfPlayer).
    /// Публічний — можна викликати вручну одразу після спавну нового
    /// союзного менеджера, не чекаючи friendlyTeamsRefreshInterval.
    /// </summary>
    public void RefreshFriendlyTeams()
    {
        friendlyTeamIDs.Clear();

        if (playerManager != null)
            friendlyTeamIDs.Add(playerManager.teamID);

        if (allyManagers != null && allyManagers.Count > 0)
        {
            for (int i = 0; i < allyManagers.Count; i++)
            {
                if (allyManagers[i] != null)
                    friendlyTeamIDs.Add(allyManagers[i].teamID);
            }

            return;
        }

        BatalionManagerScr[] allManagers = FindObjectsOfType<BatalionManagerScr>();

        for (int i = 0; i < allManagers.Length; i++)
        {
            BatalionManagerScr manager = allManagers[i];

            if (manager != null && manager.isAllyOfPlayer)
                friendlyTeamIDs.Add(manager.teamID);
        }
    }

    public bool IsFriendlyTeam(int teamID)
    {
        return friendlyTeamIDs.Contains(teamID);
    }

    public void RecalculateVisibility()
    {
        IReadOnlyList<BattalionScr> all = BattalionScr.AllActive;

        spottersBuffer.Clear();
        targetsBuffer.Clear();

        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr battalion = all[i];

            if (battalion == null)
                continue;

            if (IsFriendlyTeam(battalion.teamID))
                spottersBuffer.Add(battalion);
            else
                targetsBuffer.Add(battalion);
        }

        RecalculateBattalionVisibility();
        RecalculateTerrainFog();
    }

    private void RecalculateBattalionVisibility()
    {
        for (int t = 0; t < targetsBuffer.Count; t++)
        {
            BattalionScr target = targetsBuffer[t];

            bool spotted = IsPositionSpottedByAny(target.transform.position);

            target.SetFogVisible(spotted);
        }
    }

    private void RecalculateTerrainFog()
    {
        IReadOnlyList<TerrainTileScr> tiles = TerrainTileScr.AllActive;

        for (int t = 0; t < tiles.Count; t++)
        {
            TerrainTileScr tile = tiles[t];

            if (tile == null)
                continue;

            bool revealed = IsPositionSpottedByAny(tile.transform.position);

            tile.SetFogRevealed(revealed);
        }
    }

    private bool IsPositionSpottedByAny(Vector2 position)
    {
        for (int s = 0; s < spottersBuffer.Count; s++)
        {
            if (IsPositionSpottedBy(spottersBuffer[s], position))
                return true;
        }

        return false;
    }

    private bool IsPositionSpottedBy(BattalionScr spotter, Vector2 position)
    {
        Vector2 from = spotter.transform.position;

        float distance = Vector2.Distance(from, position);
        float visionRange = spotter.GetEffectiveVisionRange();

        if (distance > visionRange)
            return false;

        if (TerrainManagerScr.Instance != null &&
            !TerrainManagerScr.Instance.HasLineOfSight(from, position))
        {
            return false;
        }

        return true;
    }
}