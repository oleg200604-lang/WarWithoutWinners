using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class BattleManagerScr : MonoBehaviour
{
    public List<BatalionManagerScr> battalionManagers;
    public bool isActive;
    public void IsSrtart()
    {
        int x=0;
        for (int i = 0; i< battalionManagers.Count; i++)
        {
            if (battalionManagers[i].isRedy == true)
            {
                x++;
                if (x-1 == i)
                {
                    isActive = true;
                }
            }
            print(x + "=" + i);
        }
    }

}
