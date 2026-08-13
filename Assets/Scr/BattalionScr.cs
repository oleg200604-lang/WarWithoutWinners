using Unity.VisualScripting;
using UnityEngine;

public class BattalionScr : MonoBehaviour
{
    public string name;
    public BatalionManagerScr batalionManager;
    public Personnel personnel;
    public Command[] command;
    public int teamID;
    private void Start()
    {
        name = "Infantry " + Random.Range(0,100).ToString();
    }
    private void OnMouseDown()
    {
        if (teamID == batalionManager.teamID)
        {
            if (batalionManager.selectBattalion == gameObject.GetComponent<BattalionScr>())
            {
                batalionManager.selectBattalion = null;
            }
            else if (batalionManager.selectBattalion == null)
            {
                batalionManager.selectBattalion = gameObject.GetComponent<BattalionScr>();
            }
            else
            {
                batalionManager.selectBattalion = gameObject.GetComponent<BattalionScr>();
            }
            print(name);
        }
    }
}

[System.Serializable]
public class Command
{
    public CommandType commandType;
    
}
[System.Serializable]
public class Personnel
{
    public int personnelMax;
    public int combatCapable;
    public int combatCapableNo;
    public int experience;
    public int organization, organizationMax;
}

public enum CommandType
{
    None, Move
}