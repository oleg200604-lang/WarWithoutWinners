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
    public Battalion battalion;

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

        if (battleManager != null)
        {
            lastExecutedTurn = battleManager.turnId;
        }
    }

    private void OnMouseDown()
    {
        batalionManager.SelectBattalion(this);
    }

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
                origin += attack.direction * attack.range;
            }
        }

        return origin;
    }

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
                used += attack.range * 2f;
                point += attack.direction * attack.range;
            }
        }

        return Mathf.Max(0f, speed - used);
    }

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

    public bool SetAttackOrder(
        int slot,
        Vector3 direction,
        float range)
    {
        if (slot < 0 || slot >= command.Length)
            return false;

        if (direction.sqrMagnitude < 0.001f)
            return false;

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

    private void ClearAllOrders()
    {
        for (int i = 0; i < command.Length; i++)
        {
            command[i] = new MoveCommand();
        }
    }

    public void TakeDamage(float damage)
    {
        print(damage);

        personnel.Losses(1, 9, damage);
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
                        hitTarget.TakeDamage(battalion.damage * (float)(personnel.personnelMax / (personnel.combatCapable + (personnel.combatCapableNo/2))) * (float)(personnel.organizationMax/personnel.organization));
                        Debug.Log(battalion.damage +" "+ (float)(personnel.personnelMax / (personnel.combatCapable + (personnel.combatCapableNo / 2))) + " " + (float)(personnel.organizationMax / personnel.organization));
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
    public void Losses(float deadRatio, float earlyRatio, float damage)
    {
        if (damage <= 0)
            return;

        int damageAmount = (int)damage;

        if (damageAmount <= 0)
            return;


        if (combatCapable <= 0)
        {
            int killedEarly = System.Math.Min(damageAmount, combatCapableNo);
            combatCapableNo -= killedEarly;
            return;
        }


        int actualDamage = System.Math.Min(damageAmount, combatCapable);

        float ratioSum = deadRatio + earlyRatio;

        if (ratioSum <= 0)
            return;

        int newDead = (int)(actualDamage * deadRatio / ratioSum);

        int newEarly = actualDamage - newDead;


        int earlyToDead = 0;

        if (combatCapableNo > 0)
        {
            earlyToDead = System.Math.Min(newDead, combatCapableNo);

            combatCapableNo -= earlyToDead;
        }


        combatCapable -= actualDamage;


        combatCapableNo += newEarly;

    }
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
    public float speed;
}

public enum BattalionType
{
    none, infantry, artillery, cavalry, mechanically
}