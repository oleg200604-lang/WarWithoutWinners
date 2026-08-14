using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Виділення батальйону та ввід гравця (вибір наказу, встановлення точки,
/// запуск ходу). Жодної візуалізації тут немає — див. RangeIndicatorScr
/// та BattalionVisualsScr.
/// </summary>
public class BatalionManagerScr : MonoBehaviour
{
    public int teamID;

    public int[] teamEnemyID;
    public int[] teamAllyID;
    public BattalionScr selectBattalion;
    public BattleManagerScr battleManager;
    public List<Regiment> regiment;
    // Який наказ (0, 1, 2) зараз редагується клавішами 1/2/3

    public CommandType commandType;
    public GameObject commandPanel;
    private int commandDuty;
    public bool isRedy;
    public int CommandDuty => commandDuty; // читання ззовні для візуалізації

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

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {

            isRedy = true;
            battleManager.IsSrtart();
            selectBattalion = null;
            commandPanel.SetActive(false);
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
}

[System.Serializable]
public class Regiment
{
    public string nameRegiment;
    public List<BattalionScr> battalions;
}