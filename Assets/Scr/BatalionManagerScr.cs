using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class BatalionManagerScr : MonoBehaviour
{
    public int teamID;

    public int[] teamEnemyID;
    public int[] teamAllyID;
    public BattalionScr selectBattalion;

    public List <Regiment> regiment;

    private void OnMouseDown()
    {
        Vector3 mousePosition = Input.mousePosition;

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
    }
}
[System.Serializable]
public class Regiment
{
    public BattalionScr[] battalions;
}
