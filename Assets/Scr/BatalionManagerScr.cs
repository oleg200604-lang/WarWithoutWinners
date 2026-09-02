using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatalionManagerScr : MonoBehaviour
{

    public int teamID;

    public BattalionScr selectBattalion;
    public Regiment selectRegiment;
    public BattleManagerScr battleManager;
    public BattalionUIManagerScr battalionUIManager;
    public List<Regiment> regiments;
    public CommandType commandType;
    public Ressurs ressurs = new Ressurs();
    [Tooltip("Пункти поповнення боєприпасів (Void), що належать цій команді.")]
    public List<AmmoDepotScr> ammoDepots;
    private int commandDuty;
    public bool isRedy;
    public int CommandDuty => commandDuty;

    [Header("Налаштування полку/ланцюга (застосовуються до НОВОСТВОРЕНИХ полків)")]
    [Tooltip("Максимальна відстань між сусідніми (за ланцюгом) батальйонами в новому полку.")]
    public float defaultChainMaxDistance = 6f;

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            commandDuty = 0;
            print("Наказ 1");
            if (battalionUIManager != null)
                battalionUIManager.CheckButtalion();
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            commandDuty = 1;
            print("Наказ 2");
            if (battalionUIManager != null)
                battalionUIManager.CheckButtalion();
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            commandDuty = 2;
            print("Наказ 3");
            if (battalionUIManager != null)
                battalionUIManager.CheckButtalion();
        }
        switch (commandType)
        {
            case CommandType.None:
                break;

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


        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            NextMove();
        }
    }
    public void NextMove()
    {
        ressurs.command += ressurs.planning;
        if (ressurs.command > ressurs.commandMax)
        {
            ressurs.command = ressurs.commandMax;
        }

        ResupplyAmmoDepots();

        isRedy = true;
        battleManager.IsSrtart();
        selectBattalion = null;
        selectRegiment = null;
        battalionUIManager.CommandPanel(false);
    }

    // Поповнення боєприпасами всіх батальйонів команди, що зараз стоять
    // у зоні дії будь-якого з "Void" пунктів поповнення (ammoDepots).
    // Витрачає глобальний запас ressurs.supplies.
    private void ResupplyAmmoDepots()
    {
        if (ammoDepots == null)
            return;

        for (int i = 0; i < ammoDepots.Count; i++)
        {
            if (ammoDepots[i] != null)
                ammoDepots[i].ResupplyInRange(this);
        }
    }

    // Поповнює особовий склад батальйону з глобального людського ресурсу
    // (ressurs.personnel), лише якщо боєздатні+небоєздатні < максимуму.
    // Частина досвіду розмазується на новобранців (їхній досвід = 0).
    // Повертає фактично додану кількість людей.
    public int ReinforceBattalion(BattalionScr battalion, int amount)
    {
        if (battalion == null || amount <= 0)
            return 0;

        int missing = battalion.GetMissingPersonnel();

        if (missing <= 0)
        {
            print(battalion.nameBattalion + ": особовий склад уже повний.");
            return 0;
        }

        int affordable = Mathf.Min(amount, missing, ressurs.personnel);

        if (affordable <= 0)
        {
            print("Недостатньо людського ресурсу для поповнення.");
            return 0;
        }

        int actuallyAdded = battalion.ReinforcePersonnel(affordable);

        ressurs.personnel -= actuallyAdded;

        return actuallyAdded;
    }

    private void Move()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (selectBattalion == null && selectRegiment == null)
            return;

        if (!HasEnoughCommand())
        {
            print("Недостатньо командного ресурсу для наказу.");
            return;
        }

        Vector3 mousePosition =
            Mouse.current.position.ReadValue();

        Vector3 worldPosition =
            Camera.main.ScreenToWorldPoint(mousePosition);

        worldPosition.z = 0f;

        if (selectRegiment != null)
        {
            if (selectRegiment.IssueMoveOrder(commandDuty, worldPosition))
            {
                print("Наказ руху полку встановлено.");
                AdvanceCommandDuty();
            }
            else
            {
                print("Не вдалося встановити наказ руху полку.");
            }

            return;
        }

        // Якщо цей батальйон є членом полку — обрізаємо ціль руху
        // відстанню до сусідів по ланцюгу, навіть коли керуємо ним
        // напряму (не через сам полк). Інакше окремий член полку міг
        // проігнорувати chainMaxDistance і піти будь-куди.
        Vector3 clampedTarget = worldPosition;

        if (selectBattalion.regimentredID >= 0 &&
            selectBattalion.regimentredID < regiments.Count)
        {
            Regiment ownRegiment = regiments[selectBattalion.regimentredID];
            clampedTarget = ownRegiment.ClampToChainNeighbors(selectBattalion, worldPosition, commandDuty);
        }

        if (selectBattalion.SetMoveOrder(
            commandDuty,
            clampedTarget))
        {
            print(
                "Наказ руху встановлено."
            );

            AdvanceCommandDuty();
        }
        else
        {
            print(
                "Не вдалося встановити наказ руху."
            );
        }
    }

    private void Attack()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (selectBattalion == null && selectRegiment == null)
            return;

        if (!HasEnoughCommand())
        {
            print("Недостатньо командного ресурсу для наказу.");
            return;
        }

        Vector3 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.z = 0;

        if (selectRegiment != null)
        {
            Vector3 regimentOrigin = selectRegiment.anchor;
            Vector3 regimentDirection = worldPosition - regimentOrigin;
            regimentDirection.z = 0;

            if (regimentDirection.sqrMagnitude < 0.001f)
                return;

            float regimentDesiredDistance = regimentDirection.magnitude;
            regimentDirection.Normalize();

            if (selectRegiment.IssueAttackOrder(commandDuty, regimentDirection, regimentDesiredDistance))
            {
                print("Наказ атаки полку встановлено.");
                AdvanceCommandDuty();
            }
            else
            {
                print("Не вдалося встановити наказ атаки полку.");
            }

            return;
        }

        Vector3 origin = selectBattalion.GetOrderOrigin(commandDuty);
        Vector3 direction = worldPosition - origin;
        direction.z = 0;

        if (direction.sqrMagnitude < 0.001f)
            return;

        float desiredMoveDistance = direction.magnitude;
        direction.Normalize();

        if (selectBattalion.SetAttackOrder(commandDuty, direction, desiredMoveDistance))
        {
            print("Наказ атаки встановлено.");
            AdvanceCommandDuty();
        }
        else
        {
            print("Не вдалося встановити наказ атаки — можливо, точка зайнята іншим батальйоном");
        }
    }

    private void Defend()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (selectBattalion == null && selectRegiment == null)
            return;

        if (!HasEnoughCommand())
        {
            print("Недостатньо командного ресурсу для наказу.");
            return;
        }

        Vector3 mousePosition =
            Mouse.current.position.ReadValue();

        Vector3 worldPosition =
            Camera.main.ScreenToWorldPoint(mousePosition);

        worldPosition.z = 0;

        if (selectRegiment != null)
        {
            Vector3 regimentOrigin = selectRegiment.anchor;
            Vector3 regimentDirection = worldPosition - regimentOrigin;
            regimentDirection.z = 0;

            if (regimentDirection.sqrMagnitude < 0.001f)
                return;

            regimentDirection.Normalize();

            if (selectRegiment.IssueDefendOrder(commandDuty, regimentDirection))
            {
                print("Наказ захисту полку встановлено. Напрямок: " + regimentDirection);
                AdvanceCommandDuty();
            }
            else
            {
                print("Не вдалося встановити наказ захисту полку.");
            }

            return;
        }

        Vector3 origin =
            selectBattalion.GetOrderOrigin(commandDuty);

        // На відміну від атаки — дистанція кліку не має значення,
        // гравець задає лише напрямок захисту.
        Vector3 direction =
            worldPosition - origin;

        direction.z = 0;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        if (selectBattalion.SetDefendOrder(commandDuty, direction))
        {
            print("Наказ захисту встановлено. Напрямок: " + direction);
            AdvanceCommandDuty();
        }
        else
        {
            print("Не вдалося встановити наказ захисту");
        }
    }

    private void Deploy()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame || selectBattalion == null)
            return;

        if (!HasEnoughCommand())
        {
            print("Недостатньо командного ресурсу для наказу.");
            return;
        }

        Vector3 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.z = 0;

        Vector3 origin = selectBattalion.GetOrderOrigin(commandDuty);
        Vector3 direction = worldPosition - origin;
        direction.z = 0;

        if (selectBattalion.SetDeployOrder(commandDuty, direction))
        {
            print("Наказ розкладання/згортання встановлено.");
            AdvanceCommandDuty();
        }
        else
        {
            print("Не вдалося встановити наказ розкладання/згортання.");
        }
    }

    private void Rotate()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame || selectBattalion == null)
            return;

        if (!HasEnoughCommand())
        {
            print("Недостатньо командного ресурсу для наказу.");
            return;
        }

        Vector3 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.z = 0;

        Vector3 origin = selectBattalion.GetOrderOrigin(commandDuty);
        Vector3 direction = worldPosition - origin;
        direction.z = 0;

        if (direction.sqrMagnitude < 0.001f)
            return;

        if (selectBattalion.SetRotateOrder(commandDuty, direction))
        {
            print("Наказ зміни кута наведення встановлено.");
            AdvanceCommandDuty();
        }
        else
        {
            print("Не вдалося встановити наказ зміни кута.");
        }
    }

    private void Bombard()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame || selectBattalion == null)
            return;

        if (!HasEnoughCommand())
        {
            print("Недостатньо командного ресурсу для наказу.");
            return;
        }

        Vector3 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.z = 0;

        if (selectBattalion.SetBombardOrder(commandDuty, worldPosition))
        {
            print("Наказ обстрілу встановлено.");
            AdvanceCommandDuty();
        }
        else
        {
            print("Не вдалося встановити наказ обстрілу — точка поза зоною розкладки.");
        }
    }

    // Скільки командного ресурсу коштує наказ поточному виділенню.
    // Полк рахується як ОДИН батальйон — береться вартість першого
    // батальйона полку (не сума по всіх його батальйонах).
    private int GetCurrentCommandCost()
    {
        if (selectRegiment != null)
        {
            if (selectRegiment.battalions == null || selectRegiment.battalions.Count == 0)
                return 0;

            for (int i = 0; i < selectRegiment.battalions.Count; i++)
            {
                if (selectRegiment.battalions[i] != null)
                    return selectRegiment.battalions[i].battalion.commandCost;
            }

            return 0;
        }

        if (selectBattalion != null)
            return selectBattalion.battalion.commandCost;

        return 0;
    }

    private bool HasEnoughCommand()
    {
        return ressurs.command >= GetCurrentCommandCost();
    }

    private void AdvanceCommandDuty()
    {
        ressurs.command -= GetCurrentCommandCost();

        if (ressurs.command < 0)
            ressurs.command = 0;

        if (commandDuty < 2)
        {
            commandDuty++;
            print("Наказ " + (commandDuty + 1));

            if (battalionUIManager != null)
                battalionUIManager.CheckButtalion();
        }
        else if (commandDuty >= 2)
        {
            commandDuty = 0;
            selectBattalion = null;
            selectRegiment = null;
        }
    }

    public void CreateRegiment()
    {
        if (selectBattalion == null)
        {
            Debug.LogWarning("Немає вибраного батальйону!");
            return;
        }

        Regiment newRegiment = new Regiment
        {
            nameRegiment = "Regiment",
            battalions = new List<BattalionScr>() { },
            battalionType = selectBattalion.battalion.type,
            chainMaxDistance = defaultChainMaxDistance
        };

        newRegiment.battalions.Add(selectBattalion);
        newRegiment.RecalculateFormation();

        regiments.Add(newRegiment);

        for (int i = 0; i < regiments.Count; i++)
        {
            if (regiments[i] == newRegiment)
            {
                selectBattalion.regimentredID = i;
                break;
            }
        }
        Debug.Log($"Створено полк: {newRegiment.nameRegiment}");

        battalionUIManager.RefreshRegimentButtons();
    }

    public void AddRegiment(BattalionScr battalion, Regiment regiment)
    {
        if (battalion == null || regiment == null)
        {
            print("Не вдалося додати до полку: не вибрано батальйон.");
            return;
        }

        if (battalion.battalion.type != regiment.battalionType)
        {
            print($"Не вдалося додати до полку: батальйон типу {battalion.battalion.type}, а полк — {regiment.battalionType} (полк приймає лише один тип).");
            return;
        }

        if (regiment.battalions.Contains(battalion))
        {
            print("Батальйон уже в цьому полку.");
            return;
        }

        for (int i = 0; i < regiments.Count; i++)
        {
            Regiment other = regiments[i];

            if (other != regiment && other.battalions.Contains(battalion))
            {
                RemovRegiment(battalion, other);
                break;
            }
        }

        // Умова додавання: один тип (перевірено вище) + близька відстань
        // ХОЧА Б до одного вже наявного батальйона полку — не обов'язково
        // до "кінця" списку. Порядок ланцюга після цього перебудовується
        // автоматично (RecalculateFormation -> ReorderChainByProximity),
        // тож немає ризику "перескочити" через когось, хто стоїть ближче.
        if (regiment.battalions.Count > 0)
        {
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < regiment.battalions.Count; i++)
            {
                if (regiment.battalions[i] == null)
                    continue;

                float dist = Vector3.Distance(battalion.transform.position, regiment.battalions[i].transform.position);
                nearestDistance = Mathf.Min(nearestDistance, dist);
            }

            if (nearestDistance > regiment.chainMaxDistance)
            {
                print($"Не вдалося додати до полку: задалеко від найближчого батальйона полку ({nearestDistance:0.##} > {regiment.chainMaxDistance:0.##}).");
                return;
            }
        }

        regiment.battalions.Add(battalion);

        for (int i = 0; i < regiments.Count; i++)
        {
            if (regiments[i] == regiment)
            {
                battalion.regimentredID = i;
                break;
            }
        }

        // Перебудовує і порядок ланцюга (nearest-neighbor), і anchor/offset.
        regiment.RecalculateFormation();

        if (battalionUIManager != null)
            battalionUIManager.RefreshRegimentButtons();
    }

    public void RemovRegiment(BattalionScr battalion, Regiment regiment)
    {
        if (battalion == null || regiment == null)
            return;

        if (!regiment.battalions.Contains(battalion))
            return;

        regiment.battalions.Remove(battalion);
        battalion.regimentredID = -1;
        regiment.RecalculateFormation();

        if (regiment.battalions.Count < 1)
        {
            DestroyRegiment(regiment);
        }

        if (battalionUIManager != null)
            battalionUIManager.RefreshRegimentButtons();
    }

    public void DestroyRegiment(Regiment regiment)
    {
        regiments.Remove(regiment);

        if (selectRegiment == regiment)
            selectRegiment = null;

        if (battalionUIManager != null)
            battalionUIManager.RefreshRegimentButtons();
    }

    public void SelectRegimentUnit(Regiment regiment)
    {
        commandType = CommandType.None;

        if (selectRegiment == regiment)
        {
            selectRegiment = null;
            battalionUIManager.CommandPanel(false);
        }
        else
        {
            selectBattalion = null;
            selectRegiment = regiment;
            battalionUIManager.CommandPanel(true);
        }
    }

    public void SelectBattalion(BattalionScr battalion)
    {
        commandType = CommandType.None;
        battalion.isSelect.SetActive(true);
        if (teamID == battalion.teamID)
        {
            if (selectBattalion == battalion)
            {
                selectBattalion = null;
                battalionUIManager.CommandPanel(false);
            }
            else
            {
                selectRegiment = null;
                selectBattalion = battalion;
                battalionUIManager.CommandPanel(true);
            }

            print(battalion.nameBattalion);
        }
    }

    public void SetCommandType(int type)
    {
        switch (type)
        {
            case 0:
                commandType = CommandType.None;
                break;

            case 1:
                commandType = CommandType.Move;
                break;

            case 2:
                commandType = CommandType.Attack;
                break;

            case 3:
                commandType = CommandType.Defend;
                break;

            case 4:
                commandType = CommandType.Deploy;
                break;

            case 5:
                commandType = CommandType.Rotate;
                break;

            case 6:
                commandType = CommandType.Bombard;
                break;

            default:
                commandType = CommandType.None;
                break;
        }

        if (battalionUIManager != null)
            battalionUIManager.CheckButtalion();
    }
}

[System.Serializable]
public class RegimentMember
{
    public BattalionScr battalion;
    public Vector3 offset;
}

[System.Serializable]
public class Regiment
{
    public Officer officer;
    public string nameRegiment;
    public List<BattalionScr> battalions = new List<BattalionScr>();
    public BattalionType battalionType;
    public float chainMaxDistance = 6f;

    public List<RegimentMember> members = new List<RegimentMember>();
    public Vector3 anchor;

    public void ReorderChainByProximity()
    {
        if (battalions == null || battalions.Count <= 2)
            return;

        List<BattalionScr> remaining = new List<BattalionScr>();

        for (int i = 0; i < battalions.Count; i++)
        {
            if (battalions[i] != null)
                remaining.Add(battalions[i]);
        }

        if (remaining.Count <= 2)
            return;

        List<BattalionScr> ordered = new List<BattalionScr>();

        // Починаємо саме з поточного першого елемента — щоб порядок не
        // "стрибав" довільно щоразу, коли перебудова взагалі не потрібна.
        BattalionScr current = remaining[0];
        ordered.Add(current);
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            int nearestIndex = 0;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                float dist = Vector3.Distance(current.transform.position, remaining[i].transform.position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIndex = i;
                }
            }

            current = remaining[nearestIndex];
            ordered.Add(current);
            remaining.RemoveAt(nearestIndex);
        }

        battalions = ordered;
    }

    public void RecalculateFormation()
    {
        ReorderChainByProximity();

        members.Clear();

        if (battalions == null || battalions.Count == 0)
            return;

        Vector3 center = Vector3.zero;
        int counted = 0;

        for (int i = 0; i < battalions.Count; i++)
        {
            if (battalions[i] == null)
                continue;

            center += battalions[i].transform.position;
            counted++;
        }

        if (counted == 0)
            return;

        center /= counted;
        center.z = 0f;

        anchor = center;

        for (int i = 0; i < battalions.Count; i++)
        {
            if (battalions[i] == null)
                continue;

            Vector3 offset = battalions[i].transform.position - center;
            offset.z = 0f;

            members.Add(new RegimentMember { battalion = battalions[i], offset = offset });
        }
    }

    public float GetSlowestSpeed()
    {
        float slowest = float.MaxValue;

        for (int i = 0; i < battalions.Count; i++)
        {
            if (battalions[i] == null)
                continue;

            slowest = Mathf.Min(slowest, battalions[i].battalion.speed);
        }

        return slowest == float.MaxValue ? 0f : slowest;
    }

    // Обрізає бажану ціль руху ОКРЕМОГО батальйона-члена полку відстанню
    // до його сусідів по ланцюгу (i-1, i+1 у battalions). Використовується,
    // коли гравець керує цим батальйоном НАПРЯМУ (не через сам полк) —
    // без цього наказ окремому батальйону повністю ігнорував ланцюг, і він
    // міг "втекти" від решти полку на будь-яку відстань.
    public Vector3 ClampToChainNeighbors(BattalionScr battalion, Vector3 desiredPos, int slot)
    {
        if (battalions == null)
            return desiredPos;

        int index = battalions.IndexOf(battalion);

        if (index < 0)
            return desiredPos;

        Vector3 result = desiredPos;

        if (index > 0)
            result = ClampAgainstNeighbor(result, battalions[index - 1], slot);

        if (index < battalions.Count - 1)
            result = ClampAgainstNeighbor(result, battalions[index + 1], slot);

        return result;
    }

    private Vector3 ClampAgainstNeighbor(Vector3 desiredPos, BattalionScr neighbor, int slot)
    {
        if (neighbor == null || chainMaxDistance <= 0f)
            return desiredPos;

        Vector3 neighborPos = neighbor.GetOrderOrigin(slot);
        neighborPos.z = 0f;

        Vector3 toDesired = desiredPos - neighborPos;
        toDesired.z = 0f;

        if (toDesired.magnitude > chainMaxDistance)
            return neighborPos + toDesired.normalized * chainMaxDistance;

        return desiredPos;
    }

    public bool IssueMoveOrder(int slot, Vector3 newAnchor)
    {
        if (battalions == null || battalions.Count == 0)
            return false;

        if (members.Count != battalions.Count)
            RecalculateFormation();

        newAnchor.z = 0f;

        bool anySucceeded = false;

        // Ланцюг обробляється послідовно за порядком members (== порядок
        // battalions): кожен наступний батальйон обмежений відстанню до
        // ФАКТИЧНОЇ точки зупинки попереднього (не бажаної цілі) — якщо
        // когось затримав рельєф, ланцюг не дозволить іншим "втекти" далі
        // ліміту.
        Vector3? previousResolvedPos = null;

        for (int i = 0; i < members.Count; i++)
        {
            BattalionScr battalion = members[i].battalion;

            if (battalion == null)
                continue;

            Vector3 targetPos = newAnchor + members[i].offset;
            targetPos.z = 0f;

            if (previousResolvedPos.HasValue && chainMaxDistance > 0f)
            {
                Vector3 toTarget = targetPos - previousResolvedPos.Value;
                toTarget.z = 0f;

                if (toTarget.magnitude > chainMaxDistance)
                    targetPos = previousResolvedPos.Value + toTarget.normalized * chainMaxDistance;
            }

            bool moved = battalion.SetMoveOrder(slot, targetPos);

            if (moved)
                anySucceeded = true;

            // Фактична точка, де батальйон реально стане цього ходу
            // (SetMoveOrder уже врахував рельєф і зберіг це в command[slot]).
            // Якщо наказ не вдався — лишається на своєму origin.
            Vector3 resolvedPos;

            if (battalion.command != null &&
                slot >= 0 && slot < battalion.command.Length &&
                battalion.command[slot] is MoveCommand resolvedMove &&
                resolvedMove.isSet)
            {
                resolvedPos = resolvedMove.pos;
            }
            else
            {
                resolvedPos = battalion.GetOrderOrigin(slot);
            }

            previousResolvedPos = resolvedPos;
        }

        if (anySucceeded)
            anchor = newAnchor;

        return anySucceeded;
    }

    public bool IssueAttackOrder(int slot, Vector3 direction, float desiredMoveDistance)
    {
        if (battalions == null || battalions.Count == 0)
            return false;

        bool anySucceeded = false;

        for (int i = 0; i < battalions.Count; i++)
        {
            BattalionScr battalion = battalions[i];

            if (battalion == null)
                continue;

            if (battalion.SetAttackOrder(slot, direction, desiredMoveDistance))
                anySucceeded = true;
        }

        return anySucceeded;
    }

    public bool IssueDefendOrder(int slot, Vector3 direction)
    {
        if (battalions == null || battalions.Count == 0)
            return false;

        bool anySucceeded = false;

        for (int i = 0; i < battalions.Count; i++)
        {
            BattalionScr battalion = battalions[i];

            if (battalion == null)
                continue;

            if (battalion.SetDefendOrder(slot, direction))
                anySucceeded = true;
        }

        return anySucceeded;
    }
}

[System.Serializable]
public class Ressurs
{
    public int personnel, supplies, command, commandMax,planning;
    public float discount;

    public void ByePersonnel(float price, int number)
    {
        if ((price * discount) <= command)
        {
            command -= (int)(price * discount);
            personnel += number;
        }
    }

    public void ByeSupplies(float price, int number)
    {
        if ((price * discount) <= command)
        {
            command -= (int)(price * discount);
            supplies += number;
        }
    }
}