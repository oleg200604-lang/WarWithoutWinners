using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatalionManagerScr : MonoBehaviour
{
    // =========================================================
    // TEAM
    // =========================================================

    [Header("Команда")]

    [Tooltip("Унікальний ID цієї команди.")]
    public int teamID;

    [Tooltip("ID команд, які є ворогами цієї команди.")]
    public int[] enemyID;

    [Tooltip("ID команд, які є союзниками цієї команди.")]
    public int[] allyID;


    // =========================================================
    // BATTALION / REGIMENT
    // =========================================================

    [Header("Керування")]

    public BattalionScr selectBattalion;
    public Regiment selectRegiment;

    public BattleManagerScr battleManager;
    public BattalionUIManagerScr battalionUIManager;

    public List<Regiment> regiments = new List<Regiment>();

    public CommandType commandType = CommandType.None;

    public Ressurs ressurs = new Ressurs();


    // =========================================================
    // OFFICER DATABASE
    // =========================================================
    // Єдине місце, звідки хтось (UI, батальйони, полки) отримує офіцерів.
    // OfiicerButtonScr і BattalionUIManagerScr НЕ тримають власного посилання
    // на OfficerDataBaseScr — вони йдуть сюди через GetOfficer()/OfficerCount.

    [Header("База офіцерів (єдине джерело даних)")]
    public OfficerDataBaseScr officerDataBaseScr;

    public int OfficerCount
    {
        get
        {
            return officerDataBaseScr != null
                ? officerDataBaseScr.OfficerCount
                : 0;
        }
    }

    public Officer GetOfficer(int index)
    {
        if (officerDataBaseScr == null)
            return null;

        return officerDataBaseScr.GetOfficer(index);
    }

    [Tooltip("Пункти поповнення боєприпасів.")]
    public List<AmmoDepotScr> ammoDepots =
        new List<AmmoDepotScr>();


    // =========================================================
    // COMMAND STATE
    // =========================================================

    private int commandDuty;

    public bool isRedy;

    public int CommandDuty => commandDuty;


    // =========================================================
    // REGIMENT SETTINGS
    // =========================================================

    [Header("Налаштування полку/ланцюга")]

    [Tooltip(
        "Максимальна відстань між сусідніми " +
        "за ланцюгом батальйонами."
    )]
    public float defaultChainMaxDistance = 6f;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (regiments == null)
            regiments = new List<Regiment>();

        if (ammoDepots == null)
            ammoDepots = new List<AmmoDepotScr>();
    }

    private void Update()
    {
        HandleCommandDutyInput();
        HandleCommandInput();
        HandleNextMoveInput();
    }


    // =========================================================
    // INPUT
    // =========================================================

    private void HandleCommandDutyInput()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SetCommandDuty(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SetCommandDuty(1);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SetCommandDuty(2);
        }
    }

    private void HandleCommandInput()
    {
        switch (commandType)
        {
            case CommandType.None:
                return;

            case CommandType.Move:
                Move();
                break;

            case CommandType.Attack:
                Attack();
                break;

            case CommandType.Defend:
                Defend();
                break;

            case CommandType.Deploy:
                Deploy();
                break;

            case CommandType.Rotate:
                Rotate();
                break;

            case CommandType.Bombard:
                Bombard();
                break;
        }
    }

    private void HandleNextMoveInput()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            NextMove();
        }
    }

    private void SetCommandDuty(int value)
    {
        commandDuty =
            Mathf.Clamp(value, 0, 2);

        if (battalionUIManager != null)
            battalionUIManager.CheckButtalion();
    }


    // =========================================================
    // TEAM RELATIONS
    // =========================================================

    public bool IsOwnTeam(int otherTeamID)
    {
        return teamID == otherTeamID;
    }

    public bool IsAlly(int otherTeamID)
    {
        if (otherTeamID == teamID)
            return true;

        if (allyID == null)
            return false;

        for (int i = 0; i < allyID.Length; i++)
        {
            if (allyID[i] == otherTeamID)
                return true;
        }

        return false;
    }

    public bool IsEnemy(int otherTeamID)
    {
        if (otherTeamID == teamID)
            return false;

        if (enemyID == null)
            return false;

        for (int i = 0; i < enemyID.Length; i++)
        {
            if (enemyID[i] == otherTeamID)
                return true;
        }

        return false;
    }

    public bool IsNeutral(int otherTeamID)
    {
        if (otherTeamID == teamID)
            return false;

        return !IsAlly(otherTeamID) &&
               !IsEnemy(otherTeamID);
    }

    public bool IsFriendlyTo(BatalionManagerScr other)
    {
        if (other == null)
            return false;

        return IsAlly(other.teamID);
    }

    public bool IsEnemyTo(BatalionManagerScr other)
    {
        if (other == null)
            return false;

        return IsEnemy(other.teamID);
    }


    // =========================================================
    // FOG OF WAR
    // =========================================================

    public bool CanSee(BattalionScr target)
    {
        if (target == null)
            return false;

        FogOfWarManagerScr fog =
            FogOfWarManagerScr.Instance;

        if (fog == null)
            return true;

        return fog.IsVisibleTo(this, target);
    }

    public bool IsPlayerFriendly()
    {
        FogOfWarManagerScr fog =
            FogOfWarManagerScr.Instance;

        if (fog == null ||
            fog.playerManager == null)
        {
            return false;
        }

        if (fog.playerManager == this)
            return true;

        return fog.playerManager.IsAlly(teamID) ||
               IsAlly(fog.playerManager.teamID);
    }


    // =========================================================
    // TURN
    // =========================================================

    public void NextMove()
    {
        AddPlanningCommand();

        ResupplyAmmoDepots();

        isRedy = true;

        commandType = CommandType.None;
        commandDuty = 0;

        ClearSelection();

        if (battleManager != null)
            battleManager.IsSrtart();

        if (battalionUIManager != null)
            battalionUIManager.CommandPanel(false);
    }

    private void AddPlanningCommand()
    {
        ressurs.command += ressurs.planning;

        if (ressurs.command >
            ressurs.commandMax)
        {
            ressurs.command =
                ressurs.commandMax;
        }
    }


    // =========================================================
    // AMMO
    // =========================================================

    private void ResupplyAmmoDepots()
    {
        if (ammoDepots == null)
            return;

        for (int i = ammoDepots.Count - 1;
             i >= 0;
             i--)
        {
            AmmoDepotScr depot =
                ammoDepots[i];

            if (depot == null)
            {
                ammoDepots.RemoveAt(i);
                continue;
            }

            depot.ResupplyInRange(this);
        }
    }


    // =========================================================
    // REINFORCEMENT
    // =========================================================

    public int ReinforceBattalion(
        BattalionScr battalion,
        int amount)
    {
        if (battalion == null ||
            amount <= 0)
        {
            return 0;
        }

        int missing =
            battalion.GetMissingPersonnel();

        if (missing <= 0)
            return 0;

        int available =
            Mathf.Max(
                0,
                ressurs.personnel
            );

        int requested =
            Mathf.Min(
                amount,
                missing
            );

        int actual =
            Mathf.Min(
                requested,
                available
            );

        if (actual <= 0)
            return 0;

        int added =
            battalion.ReinforcePersonnel(actual);

        if (added <= 0)
            return 0;

        ressurs.personnel -= added;

        return added;
    }


    // =========================================================
    // WORLD POSITION
    // =========================================================
    // =========================================================
    // OFFICER SYSTEM
    // =========================================================

    public bool IsOfficerAssigned(Officer officer)
    {
        if (officer == null)
            return false;

        // Перевіряємо батальйони.
        IReadOnlyList<BattalionScr> allBattalions =
            BattalionScr.AllActive;

        for (int i = 0; i < allBattalions.Count; i++)
        {
            BattalionScr battalion =
                allBattalions[i];

            if (battalion == null)
                continue;

            // Офіцер належить іншій команді.
            if (battalion.TeamID != teamID)
                continue;

            if (battalion.officer == officer ||
                battalion.officerRegiment == officer)
            {
                return true;
            }
        }

        // Додаткова перевірка безпосередньо по полках.
        if (regiments != null)
        {
            for (int i = 0; i < regiments.Count; i++)
            {
                Regiment regiment =
                    regiments[i];

                if (regiment == null)
                    continue;

                if (regiment.officer == officer)
                    return true;
            }
        }

        return false;
    }


    // =========================================================
    // CAN ASSIGN
    // =========================================================

    public bool CanAssignOfficerToCurrentSelection(
        Officer officer)
    {
        if (officer == null)
            return false;

        if (selectBattalion != null)
        {
            return CanAssignOfficerToBattalion(
                officer,
                selectBattalion
            );
        }

        if (selectRegiment != null)
        {
            return CanAssignOfficerToRegiment(
                officer,
                selectRegiment
            );
        }

        return false;
    }


    // =========================================================
    // BATTALION VALIDATION
    // =========================================================

    public bool CanAssignOfficerToBattalion(
        Officer officer,
        BattalionScr battalion)
    {
        if (officer == null ||
            battalion == null)
        {
            return false;
        }

        if (battalion.teamID != teamID)
            return false;

        if (battalion.battalion == null)
            return false;

        // Майор командує окремим батальйоном.
        if (officer.rank != Rank.Major)
            return false;

        // Тип офіцера повинен відповідати типу батальйону.
        if (officer.officetType !=
            battalion.battalion.type)
        {
            return false;
        }

        // Не можна призначити офіцера,
        // який вже командує іншим підрозділом.
        if (IsOfficerAssigned(officer))
        {
            // Якщо він уже стоїть саме тут —
            // повторне призначення не потрібне.
            if (battalion.officer == officer)
                return false;

            return false;
        }

        return true;
    }


    // =========================================================
    // REGIMENT VALIDATION
    // =========================================================

    public bool CanAssignOfficerToRegiment(
        Officer officer,
        Regiment regiment)
    {
        if (officer == null ||
            regiment == null)
        {
            return false;
        }

        if (regiment.battalions == null ||
            regiment.battalions.Count == 0)
        {
            return false;
        }

        // Майор не командує полком.
        if (officer.rank == Rank.Major)
            return false;

        // Тип офіцера повинен відповідати типу полку.
        if (officer.officetType !=
            regiment.battalionType)
        {
            return false;
        }

        // Підполковник може командувати
        // малим полком до 4 батальйонів.
        if (officer.rank ==
            Rank.Lieutenant)
        {
            if (regiment.battalions.Count > 4)
                return false;
        }

        // Один офіцер не може одночасно
        // командувати двома підрозділами.
        if (IsOfficerAssigned(officer))
            return false;

        return true;
    }


    // =========================================================
    // ASSIGN TO BATTALION
    // =========================================================

    public bool AssignOfficerToBattalion(
        Officer officer,
        BattalionScr battalion)
    {
        if (!CanAssignOfficerToBattalion(
            officer,
            battalion))
        {
            return false;
        }

        // Якщо тут вже був офіцер —
        // знімаємо його.
        if (battalion.officer != null)
        {
            Officer oldOfficer =
                battalion.officer;

            battalion.officer = null;

            battalion.RecalculateStats();
        }

        battalion.officer =
            officer;

        battalion.RecalculateStats();

        return true;
    }


    // =========================================================
    // ASSIGN TO REGIMENT
    // =========================================================

    public bool AssignOfficerToRegiment(
        Officer officer,
        Regiment regiment)
    {
        if (!CanAssignOfficerToRegiment(
            officer,
            regiment))
        {
            return false;
        }

        regiment.AssignOfficer(
            officer
        );

        return true;
    }


    // =========================================================
    // RECALCULATE OFFICER
    // =========================================================

    public void RecalculateStatsForOfficer(
        Officer officer)
    {
        if (officer == null)
            return;

        IReadOnlyList<BattalionScr> allBattalions =
            BattalionScr.AllActive;

        for (int i = 0;
             i < allBattalions.Count;
             i++)
        {
            BattalionScr battalion =
                allBattalions[i];

            if (battalion == null)
                continue;

            if (battalion.TeamID != teamID)
                continue;

            if (battalion.officer == officer ||
                battalion.officerRegiment == officer)
            {
                battalion.RecalculateStats();
            }
        }
    }
    private bool TryGetMouseWorldPosition(
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (Mouse.current == null)
            return false;

        Camera camera =
            Camera.main;

        if (camera == null)
            return false;

        Vector3 mousePosition =
            Mouse.current.position.ReadValue();

        worldPosition =
            camera.ScreenToWorldPoint(
                mousePosition
            );

        worldPosition.z = 0f;

        return true;
    }

    private bool IsRightClick()
    {
        return Mouse.current != null &&
               Mouse.current.rightButton
                   .wasPressedThisFrame;
    }


    // =========================================================
    // MOVE
    // =========================================================

    private void Move()
    {
        if (!IsRightClick())
            return;

        if (!HasSelection())
            return;

        if (!HasEnoughCommand())
            return;

        if (!TryGetMouseWorldPosition(
            out Vector3 worldPosition))
        {
            return;
        }

        if (selectRegiment != null)
        {
            if (selectRegiment.IssueMoveOrder(
                commandDuty,
                worldPosition))
            {
                CompleteCommand();
            }

            return;
        }

        Vector3 target =
            worldPosition;

        if (TryGetBattalionRegiment(
            selectBattalion,
            out Regiment regiment))
        {
            target =
                regiment.ClampToChainNeighbors(
                    selectBattalion,
                    worldPosition,
                    commandDuty
                );
        }

        if (selectBattalion.SetMoveOrder(
            commandDuty,
            target))
        {
            CompleteCommand();
        }
    }


    // =========================================================
    // ATTACK
    // =========================================================

    private void Attack()
    {
        if (!IsRightClick())
            return;

        if (!HasSelection())
            return;

        if (!HasEnoughCommand())
            return;

        if (!TryGetMouseWorldPosition(
            out Vector3 worldPosition))
        {
            return;
        }

        if (selectRegiment != null)
        {
            Vector3 direction =
                worldPosition -
                selectRegiment.anchor;

            direction.z = 0f;

            if (direction.sqrMagnitude <
                0.001f)
            {
                return;
            }

            float distance =
                direction.magnitude;

            direction.Normalize();

            if (selectRegiment.IssueAttackOrder(
                commandDuty,
                direction,
                distance))
            {
                CompleteCommand();
            }

            return;
        }

        Vector3 origin =
            selectBattalion.GetOrderOrigin(
                commandDuty
            );

        Vector3 attackDirection =
            worldPosition -
            origin;

        attackDirection.z = 0f;

        if (attackDirection.sqrMagnitude <
            0.001f)
        {
            return;
        }

        float desiredDistance =
            attackDirection.magnitude;

        attackDirection.Normalize();

        if (selectBattalion.SetAttackOrder(
            commandDuty,
            attackDirection,
            desiredDistance))
        {
            CompleteCommand();
        }
    }


    // =========================================================
    // DEFEND
    // =========================================================

    private void Defend()
    {
        if (!IsRightClick())
            return;

        if (!HasSelection())
            return;

        if (!HasEnoughCommand())
            return;

        if (!TryGetMouseWorldPosition(
            out Vector3 worldPosition))
        {
            return;
        }

        if (selectRegiment != null)
        {
            Vector3 direction =
                worldPosition -
                selectRegiment.anchor;

            direction.z = 0f;

            if (direction.sqrMagnitude <
                0.001f)
            {
                return;
            }

            direction.Normalize();

            if (selectRegiment.IssueDefendOrder(
                commandDuty,
                direction))
            {
                CompleteCommand();
            }

            return;
        }

        Vector3 origin =
            selectBattalion.GetOrderOrigin(
                commandDuty
            );

        Vector3 defendDirection =
            worldPosition -
            origin;

        defendDirection.z = 0f;

        if (defendDirection.sqrMagnitude <
            0.001f)
        {
            return;
        }

        defendDirection.Normalize();

        if (selectBattalion.SetDefendOrder(
            commandDuty,
            defendDirection))
        {
            CompleteCommand();
        }
    }


    // =========================================================
    // DEPLOY
    // =========================================================

    private void Deploy()
    {
        if (!IsRightClick())
            return;

        if (selectBattalion == null)
            return;

        if (!HasEnoughCommand())
            return;

        if (!TryGetMouseWorldPosition(
            out Vector3 worldPosition))
        {
            return;
        }

        Vector3 origin =
            selectBattalion.GetOrderOrigin(
                commandDuty
            );

        Vector3 direction =
            worldPosition -
            origin;

        direction.z = 0f;

        if (selectBattalion.SetDeployOrder(
            commandDuty,
            direction))
        {
            CompleteCommand();
        }
    }


    // =========================================================
    // ROTATE
    // =========================================================

    private void Rotate()
    {
        if (!IsRightClick())
            return;

        if (selectBattalion == null)
            return;

        if (!HasEnoughCommand())
            return;

        if (!TryGetMouseWorldPosition(
            out Vector3 worldPosition))
        {
            return;
        }

        Vector3 origin =
            selectBattalion.GetOrderOrigin(
                commandDuty
            );

        Vector3 direction =
            worldPosition -
            origin;

        direction.z = 0f;

        if (direction.sqrMagnitude <
            0.001f)
        {
            return;
        }

        direction.Normalize();

        if (selectBattalion.SetRotateOrder(
            commandDuty,
            direction))
        {
            CompleteCommand();
        }
    }


    // =========================================================
    // BOMBARD
    // =========================================================

    private void Bombard()
    {
        if (!IsRightClick())
            return;

        if (selectBattalion == null)
            return;

        if (!HasEnoughCommand())
            return;

        if (!TryGetMouseWorldPosition(
            out Vector3 worldPosition))
        {
            return;
        }

        if (selectBattalion.SetBombardOrder(
            commandDuty,
            worldPosition))
        {
            CompleteCommand();
        }
    }


    // =========================================================
    // COMMAND RESOURCE
    // =========================================================

    private int GetCurrentCommandCost()
    {
        if (selectRegiment != null)
        {
            if (selectRegiment.battalions == null)
                return 0;

            for (int i = 0;
                 i < selectRegiment.battalions.Count;
                 i++)
            {
                BattalionScr battalion =
                    selectRegiment.battalions[i];

                if (battalion == null ||
                    battalion.battalion == null)
                {
                    continue;
                }

                return battalion
                    .battalion
                    .commandCost;
            }

            return 0;
        }

        if (selectBattalion == null ||
            selectBattalion.battalion == null)
        {
            return 0;
        }

        return selectBattalion
            .battalion
            .commandCost;
    }

    private bool HasEnoughCommand()
    {
        return ressurs != null &&
               ressurs.command >=
               GetCurrentCommandCost();
    }

    private void CompleteCommand()
    {
        int cost =
            GetCurrentCommandCost();

        ressurs.command -= cost;

        if (ressurs.command < 0)
            ressurs.command = 0;

        if (commandDuty < 2)
        {
            commandDuty++;

            if (battalionUIManager != null)
                battalionUIManager.CheckButtalion();
        }
        else
        {
            commandDuty = 0;
            commandType = CommandType.None;

            ClearSelection();
        }
    }


    // =========================================================
    // SELECTION
    // =========================================================

    private bool HasSelection()
    {
        return selectBattalion != null ||
               selectRegiment != null;
    }

    private void ClearSelectionVisual()
    {
        if (selectBattalion != null &&
            selectBattalion.isSelect != null)
        {
            selectBattalion.isSelect
                .SetActive(false);
        }
    }

    public void ClearSelection()
    {
        ClearSelectionVisual();

        selectBattalion = null;
        selectRegiment = null;
        commandType = CommandType.None;

        if (battalionUIManager != null)
            battalionUIManager.CommandPanel(false);
    }

    public void SelectBattalion(
        BattalionScr battalion)
    {
        if (battalion == null)
            return;

        if (!CanSee(battalion))
            return;

        // Керувати можна тільки власними батальйонами.
        if (teamID != battalion.teamID)
            return;

        commandType =
            CommandType.None;

        if (selectBattalion == battalion)
        {
            ClearSelection();
            return;
        }

        ClearSelectionVisual();

        selectRegiment = null;
        selectBattalion = battalion;

        if (selectBattalion.isSelect != null)
        {
            selectBattalion.isSelect
                .SetActive(true);
        }

        if (battalionUIManager != null)
            battalionUIManager.CommandPanel(true);
    }

    public void SelectRegimentUnit(
        Regiment regiment)
    {
        if (regiment == null)
            return;

        commandType =
            CommandType.None;

        if (selectRegiment == regiment)
        {
            ClearSelection();
            return;
        }

        ClearSelectionVisual();

        selectBattalion = null;
        selectRegiment = regiment;

        if (battalionUIManager != null)
            battalionUIManager.CommandPanel(true);
    }


    // =========================================================
    // REGIMENT HELPERS
    // =========================================================

    private bool TryGetBattalionRegiment(
        BattalionScr battalion,
        out Regiment regiment)
    {
        regiment = null;

        if (battalion == null ||
            regiments == null)
        {
            return false;
        }

        int index =
            battalion.regimentredID;

        if (index < 0 ||
            index >= regiments.Count)
        {
            return false;
        }

        regiment =
            regiments[index];

        return regiment != null;
    }

    private int GetRegimentIndex(
        Regiment regiment)
    {
        if (regiment == null ||
            regiments == null)
        {
            return -1;
        }

        return regiments.IndexOf(regiment);
    }


    // =========================================================
    // CREATE REGIMENT
    // =========================================================

    public void CreateRegiment()
    {
        if (selectBattalion == null)
        {
            Debug.LogWarning(
                "Немає вибраного батальйону."
            );

            return;
        }

        if (selectBattalion.battalion == null)
            return;

        Regiment newRegiment =
            new Regiment
            {
                nameRegiment = "Regiment",

                battalions =
                    new List<BattalionScr>(),

                battalionType =
                    selectBattalion
                        .battalion
                        .type,

                chainMaxDistance =
                    defaultChainMaxDistance
            };

        newRegiment.battalions.Add(
            selectBattalion
        );

        regiments.Add(
            newRegiment
        );

        int regimentIndex =
            regiments.Count - 1;

        selectBattalion.regimentredID =
            regimentIndex;

        newRegiment.RecalculateFormation();

        selectRegiment =
            newRegiment;

        selectBattalion = null;

        if (battalionUIManager != null)
        {
            battalionUIManager
                .RefreshRegimentButtons();

            battalionUIManager
                .CommandPanel(true);
        }
    }


    // =========================================================
    // ADD TO REGIMENT
    // =========================================================

    public void AddRegiment(
        BattalionScr battalion,
        Regiment regiment)
    {
        if (battalion == null ||
            regiment == null)
        {
            return;
        }

        if (battalion.battalion == null)
            return;

        if (battalion.teamID != teamID)
            return;

        if (battalion.battalion.type !=
            regiment.battalionType)
        {
            Debug.Log(
                "Тип батальйону не відповідає типу полку."
            );

            return;
        }

        if (regiment.battalions == null)
        {
            regiment.battalions =
                new List<BattalionScr>();
        }

        if (regiment.battalions.Contains(
            battalion))
        {
            return;
        }

        // Видаляємо з попереднього полку.
        if (TryGetBattalionRegiment(
            battalion,
            out Regiment oldRegiment))
        {
            if (oldRegiment != regiment)
            {
                RemoveBattalionFromRegiment(
                    battalion,
                    oldRegiment
                );
            }
        }

        // Додатково шукаємо старе членство,
        // якщо regimentredID вже був пошкоджений.
        RemoveBattalionFromAnyOtherRegiment(
            battalion,
            regiment
        );

        if (regiment.battalions.Count > 0)
        {
            float nearestDistance =
                float.MaxValue;

            for (int i = 0;
                 i < regiment.battalions.Count;
                 i++)
            {
                BattalionScr other =
                    regiment.battalions[i];

                if (other == null)
                    continue;

                float distance =
                    Vector3.Distance(
                        battalion.transform.position,
                        other.transform.position
                    );

                if (distance < nearestDistance)
                    nearestDistance = distance;
            }

            if (nearestDistance >
                regiment.chainMaxDistance)
            {
                Debug.Log(
                    $"Батальйон занадто далеко від полку: " +
                    $"{nearestDistance:0.##} > " +
                    $"{regiment.chainMaxDistance:0.##}"
                );

                return;
            }
        }

        regiment.battalions.Add(
            battalion
        );

        int index =
            GetRegimentIndex(regiment);

        battalion.regimentredID =
            index;

        regiment.RecalculateFormation();

        if (battalionUIManager != null)
            battalionUIManager.RefreshRegimentButtons();
    }

    private void RemoveBattalionFromAnyOtherRegiment(
        BattalionScr battalion,
        Regiment except)
    {
        if (regiments == null)
            return;

        for (int i = regiments.Count - 1;
             i >= 0;
             i--)
        {
            Regiment regiment =
                regiments[i];

            if (regiment == null ||
                regiment == except ||
                regiment.battalions == null)
            {
                continue;
            }

            if (regiment.battalions.Contains(
                battalion))
            {
                RemoveBattalionFromRegiment(
                    battalion,
                    regiment
                );
            }
        }
    }

    private void RemoveBattalionFromRegiment(
        BattalionScr battalion,
        Regiment regiment)
    {
        if (regiment == null ||
            regiment.battalions == null)
        {
            return;
        }

        regiment.battalions.Remove(
            battalion
        );

        // Батальйон покинув полк — бонус офіцера полку більше не має діяти на нього.
        // isSelect самого офіцера НЕ чіпаємо: він і далі командує рештою полку.
        battalion.ClearOfficerRegiment();

        regiment.RecalculateFormation();

        if (regiment.battalions.Count == 0)
        {
            DestroyRegiment(regiment);
        }
    }


    // =========================================================
    // REMOVE REGIMENT
    // =========================================================

    public void RemovRegiment(
        BattalionScr battalion,
        Regiment regiment)
    {
        if (battalion == null ||
            regiment == null)
        {
            return;
        }

        if (regiment.battalions == null)
            return;

        if (!regiment.battalions.Contains(
            battalion))
        {
            return;
        }

        regiment.battalions.Remove(
            battalion
        );

        battalion.regimentredID = -1;

        regiment.RecalculateFormation();

        if (regiment.battalions.Count == 0)
        {
            DestroyRegiment(regiment);
        }

        if (battalionUIManager != null)
            battalionUIManager.RefreshRegimentButtons();
    }


    // =========================================================
    // DESTROY REGIMENT
    // =========================================================

    public void DestroyRegiment(
        Regiment regiment)
    {
        if (regiment == null)
            return;

        if (regiment.battalions != null)
        {
            for (int i = 0;
                 i < regiment.battalions.Count;
                 i++)
            {
                BattalionScr battalion =
                    regiment.battalions[i];

                if (battalion != null &&
                    battalion.regimentredID ==
                    GetRegimentIndex(regiment))
                {
                    battalion.regimentredID = -1;
                }
            }
        }

        if (selectRegiment == regiment)
        {
            selectRegiment = null;
        }

        regiments.Remove(
            regiment
        );

        // Після видалення індекси всіх полків
        // можуть зміститися.
        RebuildRegimentIndices();

        if (battalionUIManager != null)
            battalionUIManager.RefreshRegimentButtons();
    }

    private void RebuildRegimentIndices()
    {
        if (regiments == null)
            return;

        for (int i = 0;
             i < regiments.Count;
             i++)
        {
            Regiment regiment =
                regiments[i];

            if (regiment == null ||
                regiment.battalions == null)
            {
                continue;
            }

            for (int j = 0;
                 j < regiment.battalions.Count;
                 j++)
            {
                BattalionScr battalion =
                    regiment.battalions[j];

                if (battalion != null)
                    battalion.regimentredID = i;
            }
        }
    }


    // =========================================================
    // COMMAND TYPE
    // =========================================================

    public void SetCommandType(
        int type)
    {
        switch (type)
        {
            case 0:
                commandType =
                    CommandType.None;
                break;

            case 1:
                commandType =
                    CommandType.Move;
                break;

            case 2:
                commandType =
                    CommandType.Attack;
                break;

            case 3:
                commandType =
                    CommandType.Defend;
                break;

            case 4:
                commandType =
                    CommandType.Deploy;
                break;

            case 5:
                commandType =
                    CommandType.Rotate;
                break;

            case 6:
                commandType =
                    CommandType.Bombard;
                break;

            default:
                commandType =
                    CommandType.None;
                break;
        }

        if (battalionUIManager != null)
            battalionUIManager.CheckButtalion();
    }
}


// =============================================================
// REGIMENT MEMBER
// =============================================================

[System.Serializable]
public class RegimentMember
{
    public BattalionScr battalion;
    public Vector3 offset;
}


// =============================================================
// REGIMENT
// =============================================================

[System.Serializable]
public class Regiment
{
    public Officer officer;

    public string nameRegiment;

    public List<BattalionScr> battalions =
        new List<BattalionScr>();

    public BattalionType battalionType;

    public float chainMaxDistance = 6f;

    public List<RegimentMember> members =
        new List<RegimentMember>();

    public Vector3 anchor;

    // =========================================================
    // OFFICER
    // =========================================================

    public void AssignOfficer(Officer newOfficer)
    {
        if (newOfficer == null)
            return;

        // Якщо це той самий офіцер,
        // просто переконаємось, що всі батальйони
        // мають актуальне посилання.
        if (officer == newOfficer)
        {
            ApplyOfficerToBattalions();
            return;
        }

        // Старий офіцер більше не командує полком.
        officer = newOfficer;

        ApplyOfficerToBattalions();
    }


    private void ApplyOfficerToBattalions()
    {
        if (battalions == null)
            return;

        for (int i = 0;
             i < battalions.Count;
             i++)
        {
            BattalionScr battalion =
                battalions[i];

            if (battalion == null)
                continue;

            battalion.officerRegiment =
                officer;

            battalion.RecalculateStats();
        }
    }


    public void UnassignOfficer()
    {
        officer = null;

        if (battalions == null)
            return;

        for (int i = 0;
             i < battalions.Count;
             i++)
        {
            BattalionScr battalion =
                battalions[i];

            if (battalion == null)
                continue;

            battalion.ClearOfficerRegiment();
        }
    }

    public void SelectOfficer(Officer officers)
    {
        if (officers.officetType == battalionType)
        {
            if (officers.isSelect == false)
            {
                // Було: switch (officer.rank) — це звання ВЖЕ призначеного офіцера
                // (null при першому призначенні -> NullReferenceException).
                // Тут має перевірятись звання кандидата, якого призначаємо.
                switch (officers.rank)
                {
                    case Rank.Lieutenant:
                        if (battalions.Count <= 4)
                        {
                            officer = officers;
                            officer.isSelect = true;


                            for (int i = 0; i < battalions.Count; i++)
                            {
                                battalions[i].officerRegiment = officer;
                                battalions[i].RecalculateStats();
                            }
                        }
                        break;

                    case Rank.Colonel:
                        officer = officers;
                        officer.isSelect = true;

                        for (int i = 0; i < battalions.Count; i++)
                        {
                            battalions[i].officerRegiment = officer;
                            battalions[i].RecalculateStats();
                        }
                        break;

                    case Rank.General:
                        officer = officers;
                        officer.isSelect = true;

                        for (int i = 0; i < battalions.Count; i++)
                        {
                            battalions[i].officerRegiment = officer;
                            battalions[i].RecalculateStats();
                        }
                        break;
                }

            }
            else
            {
                if (officers.rank == Rank.General)
                {
                    officer = officers;
                    for (int i = 0; i < battalions.Count; i++)
                    {
                        battalions[i].officerRegiment = officer;
                        battalions[i].RecalculateStats();
                    }
                }
            }

        }
    }

    // =========================================================
    // CHAIN ORDER
    // =========================================================

    public void ReorderChainByProximity()
    {
        CleanupNullBattalions();

        if (battalions.Count <= 2)
            return;

        List<BattalionScr> remaining =
            new List<BattalionScr>(
                battalions
            );

        List<BattalionScr> ordered =
            new List<BattalionScr>();

        BattalionScr current =
            remaining[0];

        ordered.Add(current);
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            int nearestIndex = 0;

            float nearestDistance =
                float.MaxValue;

            Vector3 currentPosition =
                current.transform.position;

            for (int i = 0;
                 i < remaining.Count;
                 i++)
            {
                BattalionScr candidate =
                    remaining[i];

                float distance =
                    (candidate.transform.position -
                     currentPosition)
                    .sqrMagnitude;

                if (distance <
                    nearestDistance)
                {
                    nearestDistance =
                        distance;

                    nearestIndex =
                        i;
                }
            }

            current =
                remaining[nearestIndex];

            ordered.Add(current);

            remaining.RemoveAt(
                nearestIndex
            );
        }

        battalions =
            ordered;
    }


    // =========================================================
    // FORMATION
    // =========================================================

    public void RecalculateFormation()
    {
        if (battalions == null)
        {
            battalions =
                new List<BattalionScr>();
        }

        if (members == null)
        {
            members =
                new List<RegimentMember>();
        }

        CleanupNullBattalions();

        ReorderChainByProximity();

        members.Clear();

        if (battalions.Count == 0)
            return;

        Vector3 center =
            Vector3.zero;

        for (int i = 0;
             i < battalions.Count;
             i++)
        {
            center +=
                battalions[i]
                    .transform.position;
        }

        center /=
            battalions.Count;

        center.z = 0f;

        anchor =
            center;

        for (int i = 0;
             i < battalions.Count;
             i++)
        {
            BattalionScr battalion =
                battalions[i];

            Vector3 offset =
                battalion.transform.position -
                center;

            offset.z = 0f;

            members.Add(
                new RegimentMember
                {
                    battalion = battalion,
                    offset = offset
                }
            );
        }
    }

    private void CleanupNullBattalions()
    {
        if (battalions == null)
        {
            battalions =
                new List<BattalionScr>();

            return;
        }

        for (int i = battalions.Count - 1;
             i >= 0;
             i--)
        {
            if (battalions[i] == null)
                battalions.RemoveAt(i);
        }
    }


    // =========================================================
    // SPEED
    // =========================================================

    public float GetSlowestSpeed()
    {
        if (battalions == null ||
            battalions.Count == 0)
        {
            return 0f;
        }

        float slowest =
            float.MaxValue;

        for (int i = 0;
             i < battalions.Count;
             i++)
        {
            BattalionScr battalion =
                battalions[i];

            if (battalion == null ||
                battalion.battalion == null)
            {
                continue;
            }

            slowest =
                Mathf.Min(
                    slowest,
                    battalion.battalion.speed
                );
        }

        return slowest ==
               float.MaxValue
            ? 0f
            : slowest;
    }


    // =========================================================
    // CHAIN CLAMP
    // =========================================================

    public Vector3 ClampToChainNeighbors(
        BattalionScr battalion,
        Vector3 desiredPos,
        int slot)
    {
        if (battalion == null ||
            battalions == null ||
            battalions.Count == 0)
        {
            return desiredPos;
        }

        int index =
            battalions.IndexOf(
                battalion
            );

        if (index < 0)
            return desiredPos;

        Vector3 result =
            desiredPos;

        if (index > 0)
        {
            result =
                ClampAgainstNeighbor(
                    result,
                    battalions[index - 1],
                    slot
                );
        }

        if (index <
            battalions.Count - 1)
        {
            result =
                ClampAgainstNeighbor(
                    result,
                    battalions[index + 1],
                    slot
                );
        }

        result.z = 0f;

        return result;
    }

    private Vector3 ClampAgainstNeighbor(
        Vector3 desiredPos,
        BattalionScr neighbor,
        int slot)
    {
        if (neighbor == null ||
            chainMaxDistance <= 0f)
        {
            return desiredPos;
        }

        Vector3 neighborPos =
            neighbor.GetOrderOrigin(slot);

        neighborPos.z = 0f;

        Vector3 difference =
            desiredPos -
            neighborPos;

        difference.z = 0f;

        float distance =
            difference.magnitude;

        if (distance <=
            chainMaxDistance)
        {
            return desiredPos;
        }

        if (distance <= 0.001f)
            return neighborPos;

        return
            neighborPos +
            difference.normalized *
            chainMaxDistance;
    }


    // =========================================================
    // MOVE ORDER
    // =========================================================

    public bool IssueMoveOrder(
        int slot,
        Vector3 newAnchor)
    {
        CleanupNullBattalions();

        if (battalions.Count == 0)
            return false;

        if (members.Count !=
            battalions.Count)
        {
            RecalculateFormation();
        }

        newAnchor.z = 0f;

        bool anySucceeded = false;

        Vector3? previousResolvedPos =
            null;

        for (int i = 0;
             i < members.Count;
             i++)
        {
            RegimentMember member =
                members[i];

            BattalionScr battalion =
                member.battalion;

            if (battalion == null)
                continue;

            Vector3 targetPos =
                newAnchor +
                member.offset;

            targetPos.z = 0f;

            if (previousResolvedPos.HasValue &&
                chainMaxDistance > 0f)
            {
                Vector3 difference =
                    targetPos -
                    previousResolvedPos.Value;

                difference.z = 0f;

                float distance =
                    difference.magnitude;

                if (distance >
                    chainMaxDistance)
                {
                    if (distance > 0.001f)
                    {
                        targetPos =
                            previousResolvedPos.Value +
                            difference.normalized *
                            chainMaxDistance;
                    }
                }
            }

            bool success =
                battalion.SetMoveOrder(
                    slot,
                    targetPos
                );

            if (success)
                anySucceeded = true;

            previousResolvedPos =
                GetResolvedMovePosition(
                    battalion,
                    slot
                );
        }

        if (anySucceeded)
            anchor = newAnchor;

        return anySucceeded;
    }

    private Vector3 GetResolvedMovePosition(
        BattalionScr battalion,
        int slot)
    {
        if (battalion != null &&
            battalion.command != null &&
            slot >= 0 &&
            slot < battalion.command.Length)
        {
            if (battalion.command[slot]
                is MoveCommand move &&
                move.isSet)
            {
                return move.pos;
            }
        }

        if (battalion != null)
            return battalion.GetOrderOrigin(slot);

        return Vector3.zero;
    }


    // =========================================================
    // ATTACK ORDER
    // =========================================================

    public bool IssueAttackOrder(
        int slot,
        Vector3 direction,
        float desiredMoveDistance)
    {
        CleanupNullBattalions();

        if (battalions.Count == 0)
            return false;

        if (direction.sqrMagnitude <
            0.001f)
        {
            return false;
        }

        direction.z = 0f;
        direction.Normalize();

        bool anySucceeded = false;

        for (int i = 0;
             i < battalions.Count;
             i++)
        {
            BattalionScr battalion =
                battalions[i];

            if (battalion == null)
                continue;

            if (battalion.SetAttackOrder(
                slot,
                direction,
                desiredMoveDistance))
            {
                anySucceeded = true;
            }
        }

        return anySucceeded;
    }


    // =========================================================
    // DEFEND ORDER
    // =========================================================

    public bool IssueDefendOrder(
        int slot,
        Vector3 direction)
    {
        CleanupNullBattalions();

        if (battalions.Count == 0)
            return false;

        if (direction.sqrMagnitude <
            0.001f)
        {
            return false;
        }

        direction.z = 0f;
        direction.Normalize();

        bool anySucceeded = false;

        for (int i = 0;
             i < battalions.Count;
             i++)
        {
            BattalionScr battalion =
                battalions[i];

            if (battalion == null)
                continue;

            if (battalion.SetDefendOrder(
                slot,
                direction))
            {
                anySucceeded = true;
            }
        }

        return anySucceeded;
    }
}

[System.Serializable]
public class Ressurs
{
    public int personnel;
    public int supplies;
    public int command;
    public int commandMax;
    public int planning;

    public float discount;

    public void ByePersonnel(
        float price,
        int number)
    {
        if (number <= 0)
            return;

        int cost =
            Mathf.Max(
                0,
                (int)(price * discount)
            );

        if (cost <= command)
        {
            command -= cost;
            personnel += number;
        }
    }

    public void ByeSupplies(
        float price,
        int number)
    {
        if (number <= 0)
            return;

        int cost =
            Mathf.Max(
                0,
                (int)(price * discount)
            );

        if (cost <= command)
        {
            command -= cost;
            supplies += number;
        }
    }
}