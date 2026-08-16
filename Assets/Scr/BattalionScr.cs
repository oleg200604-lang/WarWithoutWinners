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

    [Header("Атака / Захист")]
    [Tooltip("Фіксована дальність — НЕ залежить від того, скільки Speed вже витрачено іншими наказами в черзі.")]
    public float attackRange = 2.5f;

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

        // Фіксована дальність — раніше тут стояло GetRemainingRange(slot)/2f,
        // через що атака ставала слабшою залежно від того, скільки Speed
        // вже витратили попередні накази в черзі. Тепер це незалежний ліміт.
        if (range > attackRange)
        {
            range = attackRange;
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
    /// Захист — гравець задає лише напрямок, батальйон нікуди не рухається
    /// і Speed не витрачає. Дальність зони — та сама фіксована attackRange,
    /// що й в атаки, незалежно від залишку Speed.
    /// </summary>
    public bool SetDefendOrder(int slot, Vector3 direction)
    {
        if (slot < 0 || slot >= command.Length)
            return false;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        if (attackRange <= 0f)
            return false;

        DefendOrder defend = new DefendOrder();

        defend.direction = direction.normalized;
        defend.range = attackRange;
        defend.commandType = CommandType.Defend;
        defend.isSet = true;

        command[slot] = defend;

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

    /// <summary>Спільна формула шкоди для Attack і Defend — щоб не дублювати той самий вираз двічі.</summary>
    private float ComputeAttackDamage()
    {
        return battalion.damage
            * (float)(personnel.personnelMax / (personnel.combatCapable + (personnel.combatCapableNo / 2)))
            * (float)(personnel.organizationMax / personnel.organization);
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

                if (attackSystem == null)
                {
                    Debug.LogWarning(nameBattalion + ": attackSystem не призначено — атака рухає батальйон, але не завдає шкоди.", this);
                }

                bool hasHit = false;
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

                    // Перевіряємо щокадру, поки батальйон іде вперед — а не
                    // лише один раз на старті. Дальність променя щоразу
                    // береться від ПОТОЧНОЇ позиції до кінця шляху.
                    if (!hasHit && attackSystem != null)
                    {
                        float remainingDistance = Vector3.Distance(transform.position, target);
                        BattalionScr hitTarget = attackSystem.FindTarget(this, attack.direction, remainingDistance);

                        if (hitTarget != null)
                        {
                            float damage = ComputeAttackDamage();
                            hitTarget.TakeDamage(damage);
                            print(nameBattalion + ": атака влучила по " + hitTarget.nameBattalion);
                            hasHit = true;
                        }
                    }

                    yield return null;
                }

                transform.position = target;

                if (attackSystem != null && !hasHit)
                {
                    print(nameBattalion + ": атака нікого не зачепила");
                }

                print("Attack: direction = " + attack.direction + ", range = " + attack.range);
            }
            else if (command[i] is DefendOrder defend && defend.isSet)
            {
                // Захист нікуди не рухається — просто чекає весь час
                // наказу (orderDuration) і щокадру перевіряє свою зону.
                // Щойно там з'являється ворог — б'є один раз.
                bool hasFired = false;
                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;

                    if (!hasFired && attackSystem != null)
                    {
                        BattalionScr hitTarget = attackSystem.FindTarget(this, defend.direction, defend.range);

                        if (hitTarget != null)
                        {
                            float damage = ComputeAttackDamage();
                            hitTarget.TakeDamage(damage);
                            print(nameBattalion + ": захист влучив по " + hitTarget.nameBattalion);
                            hasFired = true;
                        }
                    }

                    yield return null;
                }

                if (!hasFired)
                {
                    print(nameBattalion + ": захист нікого не побачив");
                }
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
public class DefendOrder : Command
{
    public CommandType commandType;
    public Vector3 direction; // гравець визначає лише напрямок
    public float range;       // та сама фіксована attackRange, що й в атаки
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