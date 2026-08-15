using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class BattleManagerScr : MonoBehaviour
{
    public List<BatalionManagerScr> battalionManagers;
    public int turnId;

    public void IsSrtart()
    {
        for (int i = 0; i < battalionManagers.Count; i++)
        {
            if (!battalionManagers[i].isRedy)
                return; // хоч один не готовий — хід не починається
        }

        turnId++;

        for (int i = 0; i < battalionManagers.Count; i++)
        {
            battalionManagers[i].isRedy = false; // готуємо до наступного ходу
        }
    }
}
