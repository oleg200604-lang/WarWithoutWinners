using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatalionManagerScr : MonoBehaviour
{
    public int teamID;

    public RegimentSettings regimentSettings;
    public int[] teamEnemyID;
    public int[] teamAllyID;
    public BattalionScr selectBattalion;
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
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            commandDuty = 1;
            print("Наказ 2");
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            commandDuty = 2;
            print("Наказ 3");
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

            isRedy = true;
            battleManager.IsSrtart();
            selectBattalion = null;
            battalionUIManager.CommandPanel(false);
        }
    }

    private void Move()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (selectBattalion == null)
            return;

        Vector3 mousePosition =
            Mouse.current.position.ReadValue();

        Vector3 worldPosition =
            Camera.main.ScreenToWorldPoint(mousePosition);

        worldPosition.z = 0f;

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
        if (Mouse.current.rightButton.wasPressedThisFrame && selectBattalion != null)
        {
            Vector3 mousePosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
            worldPosition.z = 0;

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
    }

    private void Defend()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame &&
            selectBattalion != null)
        {
            Vector3 mousePosition =
                Mouse.current.position.ReadValue();

            Vector3 worldPosition =
                Camera.main.ScreenToWorldPoint(mousePosition);

            worldPosition.z = 0;

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

        // Один і той самий наказ перемикає Deploy/Undeploy залежно від
        // поточного isDeployed. Напрямок клацання потрібен лише коли
        // РОЗКЛАДАЄМОСЬ — SetDeployOrder сам відхилить порожній напрямок,
        // якщо він насправді потрібен, і сам ігнорує його при згортанні.
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
        }
    }

    public void CreateRegiment()
    {
        if (selectBattalion == null)
        {
            Debug.LogWarning("Немає вибраного батальйону!");
            return;
        }

        Regiment newRegiment = new Regiment { nameRegiment = "Regiment", battalions = new List<BattalionScr>() { }, battalionType = BattalionType.infantry };

        newRegiment.battalions.Add(selectBattalion);

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

        for (int i = 0; i < regiments.Count; i++)
        {
            battalionUIManager.ChekRegiment(i);
        }
    }

    public void AddRegiment(BattalionScr battalion, Regiment regiment)
    {
        if (battalion.battalion.type == regiment.battalionType)
        {
            for (int i = 0; regiments.Count > i; i++)
            {
                if (regiments[i] == regiment)
                {
                    battalion.regimentredID = i;
                }
            }
            regiment.battalions.Add(battalion);
        }
    }

    public void RemovRegiment(BattalionScr battalion, Regiment regiment)
    {
        if (battalion.battalion.type == regiment.battalionType)
        {
            regiment.battalions.Add(battalion);
        }
    }
    public void DestroyRegiment(Regiment regiment)
    {
        regiments.Remove(regiment);
    }

    public void SelectRegiment(Regiment regiment)
    {
        switch (regimentSettings)
        {
            case RegimentSettings.None:

                break;

            case RegimentSettings.Remove:
                for (int i = 0; i < regiment.battalions.Count; i++)
                {
                    if (regiment.battalions[i] == selectBattalion)
                    {
                        RemovRegiment(selectBattalion, regiment);
                        break;
                    }

                }
                break;

            case RegimentSettings.Add:
                for (int i = 0; i < regiment.battalions.Count; i++)
                {
                    if (regiment.battalions[i] == selectBattalion)
                    {
                        AddRegiment(selectBattalion, regiment);
                        break;
                    }
                }
                break;
        }
        if (regiment.battalions.Count < 1)
        {
            DestroyRegiment(regiment);
        }
        print(regiment);
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

[System.Serializable]
public class Regiment
{
    public string nameRegiment;
    public List<BattalionScr> battalions;
    public BattalionType battalionType;
}

public enum RegimentSettings
{
    None, Remove, Add
}