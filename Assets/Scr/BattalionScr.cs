using Unity.VisualScripting;
using UnityEngine;

public class BattalionScr : MonoBehaviour
{
    public bool isRun;
    public string name;
    public BatalionManagerScr batalionManager;
    public Personnel personnel;
    public Command[] command = new Command[3];
    public int teamID;
    private void Awake()
{
    command[0] = new MoveCommand();
    command[1] = new MoveCommand();
    command[2] = new MoveCommand();
}
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
    private void Update()
    {
        if (isRun == true)
        {
            for (int i = 0; i < command.Length; i++)
            {
                if (command[i] is MoveCommand move)
                {

                    transform.position = new Vector3(move.pos.x, move.pos.y, 0);
                    print(move.pos);
                    isRun = false;
                }
            }
        }
    }
}

[System.Serializable]
public class MoveCommand : Command
{
    public CommandType commandType;

    public Vector3 pos;
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

public interface Command
{

}
[System.Serializable]
public class Battalion
{
    public BattalionType type;
    public float damage;
    public float Speed;
}
public enum BattalionType
{

}