using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public int regimentredID = -1;

    [Header("Пересування")]
    public float speed = 5f;
    public float orderDuration = 1f;
    [Header("Атака / Захист")]
    [Tooltip("Фіксована дальність — НЕ залежить від того, скільки Speed вже витрачено іншими наказами в черзі.")]
    public float attackRange = 2.5f;
    [Tooltip("У скільки разів рух під час атаки дорожчий за звичайний Move (Speed за ту саму дистанцію).")]
    public float attackMoveCostMultiplier = 2f;
    [Header("Зайнятість клітинки")]
    [Tooltip("Мінімальна відстань до іншого батальйона — новий наказ (Move/Attack) не встановиться, якщо кінцева точка опиниться ближче цього значення до чужої кінцевої позиції.")]
    public float footprintRadius = 0.6f;
    [Header("Terrain sampling")]
    [SerializeField, Min(0.05f)]
    private float terrainSampleStep = 0.15f;
    private static readonly List<BattalionScr> AllBattalions = new List<BattalionScr>();

    private void OnEnable()
    {
        AllBattalions.Add(this);
    }

    private void OnDisable()
    {
        AllBattalions.Remove(this);
    }

    private void Awake()
    {
        command[0] = new MoveCommand();
        command[1] = new MoveCommand();
        command[2] = new MoveCommand();
    }
    public Vector3 GetOrderOrigin(int slot)
    {
        if (command == null)
            return transform.position;

        slot = Mathf.Clamp(slot, 0, command.Length);

        Vector3 origin = transform.position;

        for (int i = 0; i < slot; i++)
        {
            if (command[i] is MoveCommand move && move.isSet)
            {
                origin = move.pos;
            }
            else if (command[i] is AttackOrder attack && attack.isSet)
            {
                origin += attack.direction * attack.moveDistance;
            }
        }

        origin.z = 0f;
        return origin;
    }

    public float GetEffectiveAttackRange(Vector3 origin)
    {
        if (TerrainManagerScr.Instance == null)
            return attackRange;

        LandscapeType terrain = TerrainManagerScr.Instance.GetTypeAt(origin);

        return attackRange *
               TerrainManagerScr.Instance.GetAttackRangeMultiplier(terrain);
    }

    // Необов'язковий overload: залишає сумісність зі старими викликами.
    public float GetEffectiveAttackRange()
    {
        return GetEffectiveAttackRange(transform.position);
    }

    // Вартість ВСЬОГО прямого маршруту, а не тільки terrain у точці to.
    public float GetTerrainMoveCost(Vector3 from, Vector3 to)
    {
        float distance = Vector3.Distance(from, to);

        if (distance <= 0.001f)
            return 0f;

        if (TerrainManagerScr.Instance == null)
            return distance;

        int samples = Mathf.CeilToInt(distance / terrainSampleStep);
        float segmentLength = distance / samples;
        Vector3 direction = (to - from).normalized;

        float cost = 0f;

        for (int i = 0; i < samples; i++)
        {
            // Беремо середину маленького відрізка, щоб не рахувати
            // стартовий і кінцевий terrain двічі.
            Vector3 samplePoint =
                from + direction * (segmentLength * (i + 0.5f));

            LandscapeType terrain =
                TerrainManagerScr.Instance.GetTypeAt(samplePoint);

            cost += segmentLength *
                    TerrainManagerScr.Instance.GetMoveCostMultiplier(terrain);
        }

        return cost;
    }

    // Перевіряє не тільки кінцеву точку, а весь маршрут.
    public bool IsRoutePassable(Vector3 from, Vector3 to)
    {
        if (TerrainManagerScr.Instance == null)
            return true;

        float distance = Vector3.Distance(from, to);

        if (distance <= 0.001f)
            return TerrainManagerScr.Instance.IsPassable(from, battalion.type);

        int samples = Mathf.CeilToInt(distance / terrainSampleStep);
        Vector3 direction = (to - from).normalized;

        for (int i = 1; i <= samples; i++)
        {
            Vector3 point = from + direction * (distance * i / samples);

            if (!TerrainManagerScr.Instance.IsPassable(point, battalion.type))
                return false;
        }

        return true;
    }

    // Найдальша точка в заданому напрямку, яку реально можна досягти.
    // Метод використовується і SetAttackOrder, і RangeIndicatorScr.
    public float GetReachableDistance(Vector3 origin, Vector3 direction, float desiredDistance, float availableSpeed, float costMultiplier = 1f)
    {
        if (direction.sqrMagnitude < 0.001f ||
            desiredDistance <= 0f ||
            availableSpeed <= 0f)
        {
            return 0f;
        }

        direction.Normalize();

        float low = 0f;
        float high = desiredDistance;

        // Шукаємо найбільшу відстань, для якої вистачає Speed
        // і весь прямий маршрут прохідний.
        for (int i = 0; i < 14; i++)
        {
            float middle = (low + high) * 0.5f;
            Vector3 point = origin + direction * middle;

            float cost =
                GetTerrainMoveCost(origin, point) * costMultiplier;

            if (cost <= availableSpeed && IsRoutePassable(origin, point))
                low = middle;
            else
                high = middle;
        }

        return low;
    }

    public float GetRemainingRange(int slot)
    {
        // Було slot > command.Length — це помилка.
        // При slot == command.Length звернення до command[slot] уже небезпечне.
        if (command == null || slot < 0 || slot >= command.Length)
            return 0f;

        float used = 0f;
        Vector3 point = transform.position;

        for (int i = 0; i < slot; i++)
        {
            if (command[i] is MoveCommand move && move.isSet)
            {
                used += GetTerrainMoveCost(point, move.pos);
                point = move.pos;
            }
            else if (command[i] is AttackOrder attack && attack.isSet)
            {
                Vector3 target = point + attack.direction * attack.moveDistance;

                used += GetTerrainMoveCost(point, target) *
                        attackMoveCostMultiplier;

                point = target;
            }
        }

        return Mathf.Max(0f, speed - used);
    }

    public bool SetMoveOrder(int slot, Vector3 pos)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        Vector3 origin = GetOrderOrigin(slot);
        pos.z = 0f;

        if (!IsRoutePassable(origin, pos))
            return false;

        if (!IsPositionFree(pos, footprintRadius, this))
            return false;

        float movementCost = GetTerrainMoveCost(origin, pos);

        if (movementCost > GetRemainingRange(slot))
            return false;

        ClearOrdersAfter(slot);

        command[slot] = new MoveCommand
        {
            pos = pos,
            commandType = CommandType.Move,
            isSet = true
        };

        return true;
    }

    public bool SetAttackOrder(
        int slot,
        Vector3 direction,
        float desiredMoveDistance)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        if (direction.sqrMagnitude < 0.001f || attackRange <= 0f)
            return false;

        Vector3 origin = GetOrderOrigin(slot);
        direction.Normalize();

        float moveDistance = GetReachableDistance(
            origin,
            direction,
            desiredMoveDistance,
            GetRemainingRange(slot),
            attackMoveCostMultiplier
        );

        Vector3 targetPoint = origin + direction * moveDistance;
        targetPoint.z = 0f;

        if (!IsPositionFree(targetPoint, footprintRadius, this))
            return false;

        ClearOrdersAfter(slot);

        command[slot] = new AttackOrder
        {
            direction = direction,
            moveDistance = moveDistance,

            // Дальність береться в точці, де батальйон ЗАКІНЧИТЬ атаку-рух.
            zoneRange = GetEffectiveAttackRange(targetPoint),

            commandType = CommandType.Attack,
            isSet = true
        };

        return true;
    }

    public bool SetDefendOrder(int slot, Vector3 direction)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        if (direction.sqrMagnitude < 0.001f || attackRange <= 0f)
            return false;

        Vector3 origin = GetOrderOrigin(slot);

        ClearOrdersAfter(slot);

        command[slot] = new DefendOrder
        {
            direction = direction.normalized,
            range = GetEffectiveAttackRange(origin),
            commandType = CommandType.Defend,
            isSet = true
        };

        return true;
    }
    private static bool IsPositionFree(Vector3 point, float radius, BattalionScr self)
    {
        foreach (BattalionScr other in AllBattalions)
        {
            if (other == null || other == self)
                continue;

            float minDist = radius + other.footprintRadius;

            if (Vector3.Distance(other.transform.position, point) < minDist)
                return false;
        }

        return true;
    }

    private void ClearOrdersAfter(int slot)
    {
        for (int i = slot + 1; i < command.Length; i++)
        {
            command[i] = new MoveCommand();
        }
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

    private static bool IsTerrainPassable(Vector3 point, BattalionType type)
    {
        if (TerrainManagerScr.Instance == null)
            return true;

        LandscapeType land = TerrainManagerScr.Instance.GetTypeAt(point);

        if (type == BattalionType.artillery && TerrainManagerScr.Instance.BlocksArtillery(land))
            return false;

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
                Vector3 target = start + attack.direction * attack.moveDistance;

                if (attackSystem == null)
                {
                    Debug.LogWarning(nameBattalion + ": attackSystem не призначено — атака рухає батальйон, але не завдає шкоди.", this);
                }

                bool hasHit = false;
                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(start, target, t / orderDuration);

                    if (!hasHit && attackSystem != null)
                    {
                        BattalionScr hitTarget = attackSystem.FindTarget(this, attack.direction, attack.zoneRange);

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

                print("Attack: direction = " + attack.direction +
                      ", moveDistance = " + attack.moveDistance +
                      ", zoneRange = " + attack.zoneRange);
            }
            else if (command[i] is DefendOrder defend && defend.isSet)
            {
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
    public float moveDistance;
    public float zoneRange;
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