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

    private void Update()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (selectBattalion != null)
            {
                Vector3 mousePosition = Mouse.current.position.ReadValue();

                Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);

                if (selectBattalion.command[0] is MoveCommand move)
                {
                    move.pos = worldPosition;
                    selectBattalion.command[0] = move;
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
