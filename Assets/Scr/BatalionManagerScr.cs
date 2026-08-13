using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatalionManagerScr : MonoBehaviour
{
    public int teamID;
    
    public int[] teamEnemyID;
    public int[] teamAllyID;
    public BattalionScr selectBattalion;

    public List <Regiment> regiment;
    private int commandDuty;
    private void Update()
    {
        if (Keyboard.current.digit1Key.isPressed)
        {
            commandDuty = 0;
            print("Наказ 1");
        }
        if (Keyboard.current.digit2Key.isPressed)
        {
            commandDuty = 1;
            print("Наказ 2");
        }
        if (Keyboard.current.digit3Key.isPressed)
        {
            commandDuty = 2;
            print("Наказ 3");
        }


        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (selectBattalion != null)
            {
                Vector3 mousePosition = Mouse.current.position.ReadValue();

                Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);

                if (selectBattalion.command[0] is MoveCommand move)
                {
                    move.pos = worldPosition;
                    selectBattalion.command[commandDuty] = move;
                    print(move);
                }
                print(worldPosition);

            }
        }
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            selectBattalion.isRun = true;
        }
    }
}
[System.Serializable]
public class Regiment
{
    public BattalionScr[] battalions;
}
