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
    public BattalionAttackSystemScr attackSystem;

    public Personnel personnel;

    public Command[] command = new Command[3];

    public int teamID;

    [Header("Пересування")]
    public float speed = 5f;

    public float orderDuration = 1f;

    private void Awake()
    {
        command[0] = new MoveCommand();
        command[1] = new MoveCommand();
        command[2] = new MoveCommand();
    }

    private int lastExecutedTurn = -1;

    private void Start()
    {
        nameBattalion = "Infantry " + Random.Range(0, 100);

        // Якщо почати з lastExecutedTurn = -1 без цього — перший-ліпший
        // Update() кадру одразу побачить turnId(0) != -1 і стартоне
        // виконання наказів, навіть якщо ще ніхто не натиснув "Готово".
        if (battleManager != null)
            lastExecutedTurn = battleManager.turnId;
    }

    private void OnMouseDown()
    {
        batalionManager.SelectBattalion(this);
    }

    /// <summary>
    /// Точка, від якої відраховується наказ slot.
    /// Береться кінець останнього встановленого Move-наказу.
    /// </summary>
    public Vector3 GetOrderOrigin(int slot)
    {
        Vector3 origin = transform.position;

        for (int i = 0; i < slot; i++)
        {
            if (command[i] is MoveCommand move && move.isSet)
            {
                origin = move.pos;
            }
            else if (command[i] is AttackOrder attack && attack.isSet)
            {
                // Атака теж реально пересуває батальйон — наступний наказ
                // має рахуватись від точки, куди атака його довела.
                origin += attack.direction * attack.range;
            }
        }

        return origin;
    }

    /// <summary>
    /// Повертає скільки звичайного Speed залишилося
    /// для цього та наступних наказів.
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
            else if (command[i] is AttackOrder attack && attack.isSet)
            {
                // Той самий "курс 2 Speed за одиницю", що й у SetAttackOrder.
                used += attack.range * 2f;
                point += attack.direction * attack.range;
            }
        }

        return Mathf.Max(0f, speed - used);
    }

    /// <summary>
    /// Встановлює наказ Move.
    /// </summary>
    public void SetMoveOrder(int slot, Vector3 pos)
    {
        if (slot < 0 || slot >= command.Length)
            return;

        if (command[slot] is MoveCommand move)
        {
            move.pos = pos;
            move.commandType = CommandType.Move;
            move.isSet = true;
        }
    }

    /// <summary>
    /// Встановлює наказ Attack.
    /// Атака витрачає Speed у 2 рази швидше за Move.
    /// </summary>
    public bool SetAttackOrder(
        int slot,
        Vector3 direction,
        float range)
    {
        if (slot < 0 || slot >= command.Length)
            return false;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        // Атака коштує 2 Speed за одиницю дальності.
        float maxAttackRange = GetRemainingRange(slot) / 2f;

        if (range > maxAttackRange)
        {
            range = maxAttackRange;
        }

        if (range <= 0f)
            return false;

        AttackOrder attack = new AttackOrder();

        attack.direction = direction.normalized;
        attack.range = range;
        attack.commandType = CommandType.Attack;
        attack.isSet = true;

        command[slot] = attack;

        return true;
    }

    /// <summary>
    /// Скидає всю чергу наказів.
    /// </summary>
    private void ClearAllOrders()
    {
        for (int i = 0; i < command.Length; i++)
        {
            command[i] = new MoveCommand();
        }
    }

    /// <summary>
    /// Заглушка на майбутню логіку втрат — поки просто фіксує влучання.
    /// </summary>
    public void TakeDamage()
    {
        print("Damage!");
    }

    private void Update()
    {
        if (battleManager != null && battleManager.turnId != lastExecutedTurn)
        {
            lastExecutedTurn = battleManager.turnId;
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

                Vector3 target = new Vector3(
                    move.pos.x,
                    move.pos.y,
                    0
                );

                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;

                    transform.position =
                        Vector3.Lerp(
                            start,
                            target,
                            t / orderDuration
                        );

                    yield return null;
                }

                transform.position = target;

                print("Move: " + target);
            }
            else if (command[i] is AttackOrder attack && attack.isSet)
            {
                Vector3 start = transform.position;
                Vector3 target = start + attack.direction * attack.range;

                if (attackSystem != null)
                {
                    BattalionScr hitTarget = attackSystem.FindTarget(this, attack.direction, attack.range);

                    if (hitTarget != null)
                    {
                        hitTarget.TakeDamage();
                        print(nameBattalion + ": атака влучила по " + hitTarget.nameBattalion);
                    }
                    else
                    {
                        print(nameBattalion + ": атака нікого не зачепила");
                    }
                }
                else
                {
                    Debug.LogWarning(nameBattalion + ": attackSystem не призначено — атака рухає батальйон, але не завдає шкоди.", this);
                }

                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;

                    transform.position =
                        Vector3.Lerp(
                            start,
                            target,
                            t / orderDuration
                        );

                    yield return null;
                }

                transform.position = target;

                print("Attack: direction = " + attack.direction + ", range = " + attack.range);
            }
            else
            {
                // Порожній наказ.
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
    public bool isSet;
}

[System.Serializable]
public class AttackOrder : Command
{
    public CommandType commandType;
    public Vector3 direction;
    public float range;
    public bool isSet;
}

[System.Serializable]
public class Personnel
{
    public int personnelMax;
    public int combatCapable;
    public int combatCapableNo;
    public int experience;

    public int organization;
    public int organizationMax;
}

public enum CommandType
{
    None,
    Move,
    Attack,
    Defend
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
    none,
    inf
}