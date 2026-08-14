using System.Collections;
using UnityEngine;

/// <summary>
/// Дані та логіка батальйону: черга наказів, дальність, виконання ходу.
/// Жодної візуалізації тут немає — див. BattalionVisualsScr.
/// </summary>
public class BattalionScr : MonoBehaviour
{
    public bool isRun;
    public string nameBattalion;
    public BatalionManagerScr batalionManager;
    public BattleManagerScr battleManager;
    public Personnel personnel;
    public Command[] command = new Command[3];
    public int teamID;

    [Header("Пересування")]
    public float speed = 5f;          // сумарна дальність пересування на ВСІ 3 накази разом
    public float orderDuration = 1f;  // час виконання одного наказу, сек

    private void Awake()
    {
        command[0] = new MoveCommand();
        command[1] = new MoveCommand();
        command[2] = new MoveCommand();
    }

    private void Start()
    {
        nameBattalion = "Infantry " + Random.Range(0, 100).ToString();
    }

    private void OnMouseDown()
    {
        batalionManager.SelectBattalion(this);
    }

    /// <summary>
    /// Точка, від якої відраховується наказ slot — кінець останнього
    /// ВЖЕ ВІДДАНОГО наказу-пересування перед ним у черзі (або поточна
    /// позиція батальйона, якщо таких немає).
    /// </summary>
    public Vector3 GetOrderOrigin(int slot)
    {
        Vector3 origin = transform.position;
        for (int i = 0; i < slot; i++)
        {
            if (command[i] is MoveCommand move && move.isSet)
                origin = move.pos;
        }
        return origin;
    }

    /// <summary>
    /// Скільки дальності залишається на наказ slot: загальний speed мінус
    /// те, що вже "витрачено" попередніми ВІДДАНИМИ наказами пересування.
    /// </summary>
    public float GetRemainingRange(int slot)
    {
        float used = 0f;
        Vector3 point = transform.position;
        for (int i = 0; i < slot; i++)
        {
            if (command[i] is MoveCommand move && move.isSet)
            {
                used += Vector3.Distance(point, move.pos);
                point = move.pos;
            }
        }
        return Mathf.Max(0f, speed - used);
    }

    /// <summary>
    /// Викликається менеджером ПІСЛЯ перевірки, що точка в межах дальності.
    /// Якщо точка поза дальністю — сюди взагалі не приходимо, наказ не
    /// змінюється.
    /// </summary>
    public void SetMoveOrder(int slot, Vector3 pos)
    {
        if (command[slot] is MoveCommand move)
        {
            move.pos = pos;
            move.commandType = CommandType.Move;
            move.isSet = true;
        }
    }

    /// <summary>Скидає всю чергу наказів — виконується після останнього наказу.</summary>
    private void ClearAllOrders()
    {
        for (int i = 0; i < command.Length; i++)
        {
            command[i] = new MoveCommand();
        }
    }

    private void Update()
    {
        if (battleManager.isActive)
        {
            battleManager.isActive = false;
            StartCoroutine(ExecuteOrders());
        }
    }

    private IEnumerator ExecuteOrders()
    {
        for (int i = 0; i < command.Length; i++)
        {
            if (command[i] is MoveCommand move && move.isSet)
            {
                Vector3 start = transform.position;
                Vector3 target = new Vector3(move.pos.x, move.pos.y, 0);

                float t = 0f;
                while (t < orderDuration)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(start, target, t / orderDuration);
                    yield return null;
                }
                transform.position = target;
                print(target);
            }
            else
            {
                // Порожній наказ або заготовка під інші типи (Attack/Defend) —
                // все одно "тримає" однакову тривалість ходу наказу.
                yield return new WaitForSeconds(orderDuration);
            }
        }

        ClearAllOrders();
    }
}

[System.Serializable]
public class MoveCommand : Command
{
    public CommandType commandType;
    public Vector3 pos;
    public bool isSet; // чи цей наказ взагалі був відданий гравцем
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
    None, Move, Attack, Defend
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