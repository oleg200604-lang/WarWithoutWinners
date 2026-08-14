using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Дані та логіка батальйону: черга наказів, дальність пересування,
/// атака/захист по зоні ураження, виконання ходу.
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

    [Header("Атака / захист (стандарт — піхота)")]
    public float attackRange = 4f;
    public float attackAngle = 60f;   // повний кут сектора ураження, градуси
    public float blockAngleTolerance = 8f; // наскільки "на лінії" треба бути, щоб прикрити того, хто позаду

    private int lastExecutedTurn = -1;

    private void Awake()
    {
        command[0] = new MoveCommand();
        command[1] = new MoveCommand();
        command[2] = new MoveCommand();
    }

    private void Start()
    {
        nameBattalion = "Infantry " + Random.Range(0, 100).ToString();

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

    // ───────────────────────── Пересування ─────────────────────────

    /// <summary>
    /// Точка, від якої відраховується наказ slot — кінець останнього
    /// ВЖЕ ВІДДАНОГО наказу-пересування перед ним у черзі (або поточна
    /// позиція батальйона, якщо таких немає). Атака/захист батальйон
    /// не пересувають, тому на ланцюжок походження вони не впливають.
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
        command[slot] = new MoveCommand
        {
            pos = pos,
            commandType = CommandType.Move,
            isSet = true
        };
    }

    // ────────────────────── Атака / захист ──────────────────────

    public void SetAttackOrder(int slot, Vector3 direction)
    {
        command[slot] = new AttackCommand
        {
            direction = direction,
            commandType = CommandType.Attack,
            isSet = true
        };
    }

    public void SetDefendOrder(int slot, Vector3 direction)
    {
        command[slot] = new DefendCommand
        {
            direction = direction,
            commandType = CommandType.Defend,
            isSet = true
        };
    }

    public void TakeDamage()
    {
        print("Damage!");
    }

    private struct ZoneHit
    {
        public BattalionScr target;
        public bool blocked;
    }

    /// <summary>
    /// Всі батальйони (крім себе) в секторі [direction ± attackAngle/2] в
    /// межах attackRange, відсортовані від найближчого до найдальшого, з
    /// позначкою чи заблоковані іншим батальйоном, що стоїть ближче й
    /// приблизно на тій самій лінії (закриває шкоду собою).
    /// </summary>
    private List<ZoneHit> ComputeZoneHits(Vector3 direction)
    {
        Vector3 origin = transform.position;
        Vector3 dir = direction.normalized;

        List<BattalionScr> inZone = new List<BattalionScr>();
        foreach (var other in GetAllBattalions())
        {
            if (other == this)
                continue;

            Vector3 toOther = other.transform.position - origin;
            float distance = toOther.magnitude;
            if (distance < 0.001f || distance > attackRange)
                continue;

            if (Vector3.Angle(dir, toOther) <= attackAngle * 0.5f)
                inZone.Add(other);
        }

        inZone.Sort((a, b) => Vector3.Distance(origin, a.transform.position)
                              .CompareTo(Vector3.Distance(origin, b.transform.position)));

        List<ZoneHit> result = new List<ZoneHit>();
        List<BattalionScr> blockers = new List<BattalionScr>();

        foreach (var target in inZone)
        {
            Vector3 toTarget = target.transform.position - origin;
            float targetDistance = toTarget.magnitude;

            bool blocked = false;
            foreach (var blocker in blockers)
            {
                Vector3 toBlocker = blocker.transform.position - origin;
                if (toBlocker.magnitude >= targetDistance)
                    continue; // блокер має бути БЛИЖЧЕ за ціль

                if (Vector3.Angle(toBlocker, toTarget) <= blockAngleTolerance)
                {
                    blocked = true;
                    break;
                }
            }

            result.Add(new ZoneHit { target = target, blocked = blocked });
            blockers.Add(target); // сам теж може прикривати того, хто далі
        }

        return result;
    }

    /// <summary>Наносить шкоду всім незаблокованим у зоні, пише результат у консоль.</summary>
    private void FireZone(Vector3 direction)
    {
        foreach (var hit in ComputeZoneHits(direction))
        {
            if (hit.blocked)
            {
                print($"{nameBattalion}: постріл по {hit.target.nameBattalion} заблокований іншим батальйоном");
            }
            else
            {
                hit.target.TakeDamage();
                print($"{nameBattalion} влучив по {hit.target.nameBattalion}");
            }
        }
    }

    private List<BattalionScr> GetAllBattalions()
    {
        List<BattalionScr> result = new List<BattalionScr>();
        if (battleManager == null)
            return result;

        foreach (var manager in battleManager.battalionManagers)
        {
            foreach (var reg in manager.regiment)
            {
                result.AddRange(reg.battalions);
            }
        }
        return result;
    }

    // ───────────────────────── Виконання ходу ─────────────────────────

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
            else if (command[i] is AttackCommand attack && attack.isSet)
            {
                FireZone(attack.direction);
                yield return new WaitForSeconds(orderDuration);
            }
            else if (command[i] is DefendCommand defend && defend.isSet)
            {
                yield return StartCoroutine(ResolveDefend(defend));
            }
            else
            {
                // Порожній наказ — все одно "тримає" однакову тривалість ходу.
                yield return new WaitForSeconds(orderDuration);
            }
        }

        ClearAllOrders();
    }

    /// <summary>
    /// Весь час наказу стежить за зоною. Щойно туди заходить ворог, який
    /// нічим не прикритий — відкриває вогонь по всій зоні (як і Атака).
    /// Кожен конкретний ворог "спускає гачок" не більше одного разу за
    /// цей наказ — інакше стріляло б щокадру, поки він стоїть у зоні.
    /// </summary>
    private IEnumerator ResolveDefend(DefendCommand defend)
    {
        HashSet<BattalionScr> triggered = new HashSet<BattalionScr>();
        float t = 0f;

        while (t < orderDuration)
        {
            foreach (var hit in ComputeZoneHits(defend.direction))
            {
                if (hit.blocked)
                    continue;
                if (hit.target.teamID == teamID)
                    continue; // захист реагує лише на ворогів
                if (triggered.Contains(hit.target))
                    continue;

                triggered.Add(hit.target);
                print($"{nameBattalion} (захист): {hit.target.nameBattalion} увійшов у зону без прикриття — відкриваю вогонь");
                FireZone(defend.direction);
                break; // за цей кадр досить одного тригера
            }

            t += Time.deltaTime;
            yield return null;
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
}

[System.Serializable]
public class MoveCommand : Command
{
    public CommandType commandType;
    public Vector3 pos;
    public bool isSet; // чи цей наказ взагалі був відданий гравцем
}

[System.Serializable]
public class AttackCommand : Command
{
    public CommandType commandType;
    public Vector3 direction; // напрямок зони ураження
    public bool isSet;
}

[System.Serializable]
public class DefendCommand : Command
{
    public CommandType commandType;
    public Vector3 direction; // напрямок зони, за якою стежимо
    public bool isSet;
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