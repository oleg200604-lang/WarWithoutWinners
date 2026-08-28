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
    private int commandDuty;
    public bool isRedy;
    public int CommandDuty => commandDuty;

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
        isRedy = true;
        battleManager.IsSrtart();
        selectBattalion = null;
        selectRegiment = null;
        battalionUIManager.CommandPanel(false);
    }

    // Наказ "Рух". Якщо вибрано полк — наказ по черзі роздається
    // усім батальйонам полку (через формацію anchor+offset).
    // Якщо вибрано окремий батальйон — працює як раніше.
    private void Move()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (selectBattalion == null && selectRegiment == null)
            return;

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

        if (selectBattalion.SetMoveOrder(
            commandDuty,
            worldPosition))
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

    private void AdvanceCommandDuty()
    {
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

        Regiment newRegiment = new Regiment { nameRegiment = "Regiment", battalions = new List<BattalionScr>() { }, battalionType = selectBattalion.battalion.type };

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

    // battalion == null (нічого не обрано) чи тип не збігається — тихо ігноруємо,
    // UI сам вимикає interactable кнопки "+" в цих випадках.
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

        // Батальйон може перебувати лише в одному полку. Якщо він уже
        // в іншому — переносимо, а не залишаємо в обох одночасно.
        for (int i = 0; i < regiments.Count; i++)
        {
            Regiment other = regiments[i];

            if (other != regiment && other.battalions.Contains(battalion))
            {
                RemovRegiment(battalion, other);
                break;
            }
        }

        for (int i = 0; i < regiments.Count; i++)
        {
            if (regiments[i] == regiment)
            {
                battalion.regimentredID = i;
                break;
            }
        }

        regiment.battalions.Add(battalion);
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

    // Вибір полку як цілісної одиниці командування (аналог SelectBattalion,
    // тільки командує одразу всіма батальйонами всередині).
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

    // Клас: BatalionManagerScr
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

// Один член полку: батальйон + його фіксований зсув відносно
// центру формації (anchor). Зсув рахується від поточних позицій
// у момент RecalculateFormation() і зберігає "строй" при русі.
[System.Serializable]
public class RegimentMember
{
    public BattalionScr battalion;
    public Vector3 offset;
}

[System.Serializable]
public class Regiment
{
    public string nameRegiment;
    public List<BattalionScr> battalions;
    public BattalionType battalionType;

    public List<RegimentMember> members = new List<RegimentMember>();
    public Vector3 anchor;

    // Перераховує anchor (центр мас батальйонів) і offset кожного
    // батальйону відносно нього. Викликати після будь-якої зміни
    // складу полку (додали/прибрали батальйон).
    public void RecalculateFormation()
    {
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

    // Максимальна дальність полку обмежена найповільнішим батальйоном
    // (рішення з дизайну: рельєф/дебафи/тип роти можуть відрізнятись).
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

    // Головний метод: один наказ гравця -> по черзі роздається
    // кожному батальйону полку зі своїм зсувом, зберігаючи формацію.
    // Якщо котромусь батальйону наказ не вдалось встановити (зайнята
    // точка, непрохідний рельєф) — він просто лишається на місці,
    // решта полку все одно отримує наказ.
    public bool IssueMoveOrder(int slot, Vector3 newAnchor)
    {
        if (battalions == null || battalions.Count == 0)
            return false;

        if (members.Count != battalions.Count)
            RecalculateFormation();

        newAnchor.z = 0f;

        bool anySucceeded = false;

        for (int i = 0; i < members.Count; i++)
        {
            BattalionScr battalion = members[i].battalion;

            if (battalion == null)
                continue;

            Vector3 targetPos = newAnchor + members[i].offset;
            targetPos.z = 0f;

            if (battalion.SetMoveOrder(slot, targetPos))
                anySucceeded = true;
        }

        if (anySucceeded)
            anchor = newAnchor;

        return anySucceeded;
    }

    // Атака/захист полку: усі батальйони діють в один і той самий напрямок
    // (гравець може потім вручну перевизначити окремий батальйон окремо —
    // такий override триває один хід, як і вирішено раніше).
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