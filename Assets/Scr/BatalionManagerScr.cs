using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatalionManagerScr : MonoBehaviour
{
    public int teamID;

    public int[] teamEnemyID;
    public int[] teamAllyID;
    public BattalionScr selectBattalion;
    public BattleManagerScr battleManager;
    public List<Regiment> regiment;
    public CommandType commandType;
    public GameObject commandPanel;
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
        }


        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {

            isRedy = true;
            battleManager.IsSrtart();
            selectBattalion = null;
            commandPanel.SetActive(false);
        }
    }

    private void Move()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && selectBattalion != null)
        {
            Vector3 mousePosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
            worldPosition.z = 0;

            Vector3 origin = selectBattalion.GetOrderOrigin(commandDuty);
            float maxRange = selectBattalion.GetRemainingRange(commandDuty);
            float dist = Vector3.Distance(origin, worldPosition);

            if (dist <= maxRange)
            {
                selectBattalion.SetMoveOrder(commandDuty, worldPosition);
            }
            else
            {
                print("Точка поза межами дальності — наказ не встановлено");
            }
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
            }
            else
            {
                print("Не вдалося встановити наказ атаки");
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
            }
            else
            {
                print("Не вдалося встановити наказ захисту");
            }
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

        regiment.Add(newRegiment);

        Debug.Log($"Створено полк: {newRegiment.nameRegiment}");
    }

    public void AddRegiment(BattalionScr battalion, Regiment regiment)
    {
        if (battalion.battalion.type == regiment.battalionType)
        {
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
    public void DestroyRegiment(Regiment regiments)
    {
        regiment.Remove(regiments);
    }


    public void SelectBattalion(BattalionScr battalion)
    {
        commandType = CommandType.None;
        if (teamID == battalion.teamID)
        {
            if (selectBattalion == battalion)
            {
                selectBattalion = null;
                commandPanel.SetActive(false);
            }
            else
            {
                selectBattalion = battalion;
                commandPanel.SetActive(true);
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
        }

    }
}

[System.Serializable]
public class Regiment
{
    public string nameRegiment;
    public List<BattalionScr> battalions;
    public BattalionType battalionType;
}