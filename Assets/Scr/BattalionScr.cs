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
    public bool isDefending;
    public Vector3 defendDirection = Vector3.right;
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
    [Header("Артилерія: розкладка / обстріл")]
    [Tooltip("Стосується лише BattalionType.artillery. Поки true — Move і звичайний Attack недоступні (спершу Undeploy). Доступні Defend (лише в deployDirection), Bombard і Rotate.")]
    public bool isDeployed;
    [Tooltip("Напрямок фронту, зафіксований останнім Deploy/Rotate. Defend і Bombard, поки розкладена, прив'язані до цього напрямку.")]
    public Vector3 deployDirection = Vector3.right;
    [Tooltip("Дальність зони ураження в розкладеному стані. ОКРЕМЕ поле від attackRange (той діє лише під час руху/звичайної Attack).")]
    public float deployRange = 4f;
    [Tooltip("Ширина конуса зони розкладки (градуси) — Bombard має влучати в цей конус навколо deployDirection. Щоб обстріляти щось поза конусом — потрібен окремий наказ Rotate.")]
    public float deployConeAngle = 90f;
    [Tooltip("Радіус ураження одного обстрілу (Bombard) — б'є по ВСІХ ворожих батальйонах у цьому колі навколо точки влучення, а не лише по першому на промені.")]
    public float bombardRadius = 1.5f;
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
    // Клас: BattalionScr
    public Vector3 GetOrderOrigin(int slot)
    {
        if (command == null ||
            command.Length == 0)
        {
            return transform.position;
        }

        if (slot < 0 ||
            slot >= command.Length)
        {
            return transform.position;
        }

        Vector3 origin =
            transform.position;

        for (int i = 0; i < slot; i++)
        {
            if (command[i] is MoveCommand move &&
                move.isSet)
            {
                origin = move.pos;
            }
            else if (command[i] is AttackOrder attack &&
                     attack.isSet)
            {
                origin +=
                    attack.direction *
                    attack.moveDistance;
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

    // Зона розкладки: коло deployRange + конус deployConeAngle навколо
    // deployDirection. ОКРЕМА від attackRange/ConeAngle звичайної атаки.
    // Використовується для валідації точки Bombard. Щоб обстріляти щось
    // поза цим конусом — потрібен окремий наказ Rotate.
    public bool IsWithinDeployZone(Vector3 origin, Vector3 point)
    {
        Vector3 toPoint = point - origin;
        toPoint.z = 0f;

        float distance = toPoint.magnitude;

        if (distance > deployRange)
            return false;

        if (distance < 0.001f)
            return true;

        float angle = Vector3.Angle(deployDirection, toPoint);

        return angle <= deployConeAngle * 0.5f;
    }

    // Вартість ВСЬОГО прямого маршруту (з урахуванням terrain-мультиплікатора
    // кожного сегмента), а не тільки terrain у кінцевій точці to.
    // Це відновлює бафи/дебафи ландшафту (дороги, ліс, гори, річка) для
    // будь-якої точки на шляху, а не лише для origin.
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
    public float GetReachableDistance(
        Vector3 origin,
        Vector3 direction,
        float desiredDistance,
        float availableSpeed,
        float costMultiplier = 1f)
    {
        if (direction.sqrMagnitude < 0.001f)
            return 0f;

        if (desiredDistance <= 0f)
            return 0f;

        if (availableSpeed <= 0f)
            return 0f;

        direction.Normalize();

        /*
         * availableSpeed — базова (terrain-незалежна) швидкість
         * батальйону. Бюджет наказу = availableSpeed * orderDuration.
         *
         * costMultiplier використовується для Attack.
         *
         * Реальна вартість шляху рахується по ВСЬОМУ маршруту через
         * GetTerrainMoveCost — тому дороги/ліс/гори/річка впливають
         * на дальність незалежно від того, де саме на шляху вони
         * зустрічаються, а не лише в origin.
         */

        float budget =
            availableSpeed *
            orderDuration;

        float low = 0f;
        float high = desiredDistance;

        for (int i = 0; i < 16; i++)
        {
            float middle =
                (low + high) * 0.5f;

            Vector3 point =
                origin + direction * middle;

            float cost =
                GetTerrainMoveCost(origin, point) *
                costMultiplier;

            if (cost <= budget &&
                IsRoutePassable(origin, point))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    public float GetRemainingRange(int slot)
    {
        if (command == null ||
            slot < 0 ||
            slot >= command.Length)
        {
            return 0f;
        }

        // Бюджет наказу без знання кінцевої точки не може врахувати
        // terrain по шляху (це залежить від напрямку) — тому тут
        // повертаємо базовий бюджет; реальна вартість рахується під
        // час GetReachableDistance / SetMoveOrder, де відома ціль.
        return speed * orderDuration;
    }

    public bool SetMoveOrder(int slot, Vector3 pos)
    {
        if (command == null ||
            slot < 0 ||
            slot >= command.Length)
        {
            return false;
        }

        // Розкладена артилерія не рухається.
        if (GetProjectedDeployedState(slot))
            return false;

        Vector3 origin =
            GetOrderOrigin(slot);

        pos.z = 0f;

        // Перевіряємо весь маршрут на прохідність.
        if (!IsRoutePassable(origin, pos))
            return false;

        // Перевіряємо зайнятість кінцевої точки.
        if (!IsPositionFree(
            pos,
            footprintRadius,
            this))
        {
            return false;
        }

        /*
         * Вартість рахується по ВСЬОМУ маршруту (кожен сегмент
         * зважений на terrain-мультиплікатор у цій точці), а не лише
         * по terrain у origin — інакше дорога/ліс/гори/річка на
         * середині шляху ніяк не впливають на результат.
         */
        float movementCost =
            GetTerrainMoveCost(origin, pos);

        float budget =
            speed * orderDuration;

        /*
         * Не можна пройти далі,
         * ніж дозволяє terrain-adjusted бюджет.
         */
        if (movementCost > budget)
            return false;

        ClearOrdersAfter(slot);

        isDefending = false;

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
        if (command == null ||
            slot < 0 ||
            slot >= command.Length)
        {
            return false;
        }

        if (GetProjectedDeployedState(slot))
            return false;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        if (attackRange <= 0f)
            return false;

        Vector3 origin =
            GetOrderOrigin(slot);

        direction.Normalize();

        float moveDistance =
            GetReachableDistance(
                origin,
                direction,
                desiredMoveDistance,
                speed,
                attackMoveCostMultiplier
            );

        if (moveDistance <= 0.001f)
            return false;

        Vector3 targetPoint =
            origin +
            direction * moveDistance;

        targetPoint.z = 0f;

        if (!IsPositionFree(
            targetPoint,
            footprintRadius,
            this))
        {
            return false;
        }

        ClearOrdersAfter(slot);

        isDefending = false;

        command[slot] = new AttackOrder
        {
            direction = direction,
            moveDistance = moveDistance,

            zoneRange =
                GetEffectiveAttackRange(targetPoint),

            commandType = CommandType.Attack,
            isSet = true
        };

        return true;
    }

    public bool SetDefendOrder(int slot, Vector3 direction)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        Vector3 finalDirection;
        float finalRange;

        bool projectedDeployed =
            GetProjectedDeployedState(slot);

        if (projectedDeployed)
        {
            // Розкладена артилерія захищається
            // тільки в напрямку наведення.
            finalDirection =
                GetProjectedDeployDirection(slot);

            finalRange = deployRange;
        }
        else
        {
            if (direction.sqrMagnitude < 0.001f ||
                attackRange <= 0f)
            {
                return false;
            }

            Vector3 origin =
                GetOrderOrigin(slot);

            finalDirection =
                direction.normalized;

            finalRange =
                GetEffectiveAttackRange(origin);
        }

        ClearOrdersAfter(slot);

        isDefending = true;
        defendDirection = finalDirection;

        command[slot] = new DefendOrder
        {
            direction = finalDirection,
            range = finalRange,
            commandType = CommandType.Defend,
            isSet = true
        };

        return true;
    }

    // Зміна напрямку наведення гармати без повного згортання — доступно
    // лише поки артилерія вже розкладена. На відміну від Deploy, тут
    // конус старого deployDirection НЕ перевіряється — саме для цього
    // й потрібен цей наказ, коли ціль поза поточним конусом.
    public bool SetRotateOrder(int slot, Vector3 direction)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        if (battalion.type != BattalionType.artillery)
            return false;

        if (!GetProjectedDeployedState(slot))
            return false;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        ClearOrdersAfter(slot);

        isDefending = false;

        command[slot] = new RotateOrder
        {
            direction = direction.normalized,
            commandType = CommandType.Rotate,
            isSet = true
        };

        return true;
    }

    public bool GetProjectedDeployedState(int slot)
    {
        bool projectedDeployed =
            isDeployed;

        if (command == null)
            return projectedDeployed;

        if (slot < 0 ||
            slot >= command.Length)
        {
            return projectedDeployed;
        }

        for (int i = 0; i < slot; i++)
        {
            if (command[i] is DeployOrder deployOrder &&
                deployOrder.isSet)
            {
                projectedDeployed =
                    deployOrder.deploy;
            }
        }

        return projectedDeployed;
    }
    public Vector3 GetProjectedDeployDirection(int slot)
    {
        Vector3 projectedDirection =
            deployDirection;

        if (command == null)
            return projectedDirection;

        if (slot < 0 ||
            slot >= command.Length)
        {
            return projectedDirection.normalized;
        }

        for (int i = 0; i < slot; i++)
        {
            if (command[i] is DeployOrder deployOrder &&
                deployOrder.isSet)
            {
                if (deployOrder.deploy &&
                    deployOrder.direction.sqrMagnitude > 0.001f)
                {
                    projectedDirection =
                        deployOrder.direction.normalized;
                }
            }
            else if (command[i] is RotateOrder rotateOrder &&
                     rotateOrder.isSet)
            {
                if (rotateOrder.direction.sqrMagnitude > 0.001f)
                {
                    projectedDirection =
                        rotateOrder.direction.normalized;
                }
            }
        }

        return projectedDirection.normalized;
    }

    public bool SetDeployOrder(int slot, Vector3 direction)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        if (battalion.type != BattalionType.artillery)
            return false;

        // Дивимося не на фактичний стан,
        // а на стан після попередніх наказів.
        bool currentlyDeployed =
            GetProjectedDeployedState(slot);

        bool willDeploy = !currentlyDeployed;

        if (willDeploy &&
            direction.sqrMagnitude < 0.001f)
        {
            return false;
        }

        ClearOrdersAfter(slot);

        isDefending = false;

        command[slot] = new DeployOrder
        {
            deploy = willDeploy,

            direction =
                willDeploy
                    ? direction.normalized
                    : GetProjectedDeployDirection(slot),

            commandType = CommandType.Deploy,
            isSet = true
        };

        return true;
    }

    // Обстріл: б'є по ВСІХ ворожих батальйонах у bombardRadius навколо
    // targetPoint (не по першому на промені). Доступно лише розкладеній
    // артилерії, і лише в межах її зони розкладки.
    public bool SetBombardOrder(int slot, Vector3 targetPoint)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        if (battalion.type != BattalionType.artillery)
            return false;

        // ВАЖЛИВО:
        // перевіряємо стан після попередніх наказів.
        if (!GetProjectedDeployedState(slot))
            return false;

        Vector3 origin =
            GetOrderOrigin(slot);

        targetPoint.z = 0f;

        Vector3 projectedDirection =
            GetProjectedDeployDirection(slot);

        Vector3 toTarget =
            targetPoint - origin;

        toTarget.z = 0f;

        float distance =
            toTarget.magnitude;

        if (distance > deployRange)
            return false;

        if (distance > 0.001f)
        {
            float angle =
                Vector3.Angle(
                    projectedDirection,
                    toTarget.normalized
                );

            if (angle > deployConeAngle * 0.5f)
                return false;
        }

        ClearOrdersAfter(slot);

        isDefending = false;

        command[slot] = new BombardOrder
        {
            targetPoint = targetPoint,
            radius = bombardRadius,
            commandType = CommandType.Bombard,
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
    public float GetEffectiveSpeed(Vector3 origin)
    {
        float baseSpeed = speed;

        if (TerrainManagerScr.Instance == null)
            return baseSpeed;

        LandscapeType terrain =
            TerrainManagerScr.Instance.GetTypeAt(origin);

        float multiplier =
            TerrainManagerScr.Instance.GetMoveCostMultiplier(terrain);

        if (multiplier <= 0.001f)
            return baseSpeed;

        return baseSpeed / multiplier;
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
        switch (battalion.type)
        {
            case BattalionType.infantry:
                nameBattalion = "Infantry " + Random.Range(0, 100);
                break;
            case BattalionType.artillery:
                nameBattalion = "Artillery " + Random.Range(0, 100);
                break;
        }

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
        DefendOrder persistentDefend = null;

        for (int i = 0; i < command.Length; i++)
        {
            if (command[i] is DefendOrder defend &&
                defend.isSet)
            {
                persistentDefend = defend;
                break;
            }
        }

        for (int i = 0; i < command.Length; i++)
        {
            command[i] = new MoveCommand();
        }

        if (persistentDefend != null)
        {
            command[0] = persistentDefend;

            isDefending = true;
            defendDirection =
                persistentDefend.direction.normalized;
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
            if (command[i] is MoveCommand move &&
                move.isSet)
            {
                Vector3 start =
                    transform.position;

                Vector3 target =
                    new Vector3(
                        move.pos.x,
                        move.pos.y,
                        0f
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

                transform.position =
                    target;

                print(
                    "Move: " +
                    target
                );
            }
            else if (command[i] is AttackOrder attack &&
                     attack.isSet)
            {
                Vector3 start =
                    transform.position;

                Vector3 target =
                    start +
                    attack.direction *
                    attack.moveDistance;

                if (attackSystem == null)
                {
                    Debug.LogWarning(
                        nameBattalion +
                        ": attackSystem не призначено.",
                        this
                    );
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

                    if (!hasHit &&
                        attackSystem != null)
                    {
                        BattalionScr hitTarget =
                            attackSystem.FindTarget(
                                this,
                                attack.direction,
                                attack.zoneRange
                            );

                        if (hitTarget != null)
                        {
                            float damage =
                                ComputeAttackDamage();

                            hitTarget.TakeDamage(
                                damage
                            );

                            print(
                                nameBattalion +
                                ": атака влучила по " +
                                hitTarget.nameBattalion
                            );

                            hasHit = true;
                        }
                    }

                    yield return null;
                }

                transform.position =
                    target;

                if (attackSystem != null &&
                    !hasHit)
                {
                    print(
                        nameBattalion +
                        ": атака нікого не зачепила"
                    );
                }
            }
            else if (command[i] is DefendOrder defend &&
                     defend.isSet)
            {
                isDefending = true;
                defendDirection =
                    defend.direction.normalized;

                bool hasFired = false;

                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;

                    if (!hasFired &&
                        attackSystem != null)
                    {
                        BattalionScr hitTarget =
                            attackSystem.FindTarget(
                                this,
                                defendDirection,
                                defend.range
                            );

                        if (hitTarget != null)
                        {
                            float damage =
                                ComputeAttackDamage();

                            hitTarget.TakeDamage(
                                damage
                            );

                            print(
                                nameBattalion +
                                ": захист влучив по " +
                                hitTarget.nameBattalion
                            );

                            hasFired = true;
                        }
                    }

                    yield return null;
                }

                if (!hasFired)
                {
                    print(
                        nameBattalion +
                        ": захист нікого не побачив"
                    );
                }

                // НЕ видаляємо DefendOrder.
                // Він залишається активним між ходами.
            }
            else if (command[i] is DeployOrder deployOrder &&
                     deployOrder.isSet)
            {
                isDeployed =
                    deployOrder.deploy;

                if (deployOrder.deploy)
                {
                    deployDirection =
                        deployOrder.direction.normalized;
                }

                print(
                    nameBattalion +
                    (
                        isDeployed
                            ? ": розклалась, напрямок " +
                              deployDirection
                            : ": згорнулась"
                    )
                );

                yield return
                    new WaitForSeconds(
                        orderDuration
                    );
            }
            else if (command[i] is RotateOrder rotateOrder &&
                     rotateOrder.isSet)
            {
                deployDirection =
                    rotateOrder.direction.normalized;

                print(
                    nameBattalion +
                    ": змінила напрямок наведення на " +
                    deployDirection
                );

                yield return
                    new WaitForSeconds(
                        orderDuration
                    );
            }
            else if (command[i] is BombardOrder bombardOrder &&
                     bombardOrder.isSet)
            {
                if (attackSystem == null)
                {
                    Debug.LogWarning(
                        nameBattalion +
                        ": attackSystem не призначено — обстріл не завдає шкоди.",
                        this
                    );
                }
                else
                {
                    List<BattalionScr> hitTargets =
                        attackSystem.FindTargetsInRadius(
                            bombardOrder.targetPoint,
                            bombardOrder.radius,
                            this
                        );

                    if (hitTargets.Count > 0)
                    {
                        float damage =
                            ComputeAttackDamage();

                        foreach (
                            BattalionScr hitTarget
                            in hitTargets)
                        {
                            hitTarget.TakeDamage(
                                damage
                            );

                            print(
                                nameBattalion +
                                ": обстріл влучив по " +
                                hitTarget.nameBattalion
                            );
                        }
                    }
                    else
                    {
                        print(
                            nameBattalion +
                            ": обстріл нікого не зачепив"
                        );
                    }
                }

                yield return
                    new WaitForSeconds(
                        orderDuration
                    );
            }
            else
            {
                yield return
                    new WaitForSeconds(
                        orderDuration
                    );
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
    public Vector3 direction; // гравець визначає лише напрямок (окрім розкладеної артилерії — там deployDirection)
    public float range;
    public bool isSet;
}

[System.Serializable]
public class DeployOrder : Command
{
    public CommandType commandType;
    public bool deploy;       // true = розкладаємось, false = згортаємось (Undeploy)
    public Vector3 direction; // напрямок фронту; має значення лише коли deploy == true
    public bool isSet;
}

[System.Serializable]
public class RotateOrder : Command
{
    public CommandType commandType;
    public Vector3 direction; // новий напрямок наведення розкладеної гармати
    public bool isSet;
}

[System.Serializable]
public class BombardOrder : Command
{
    public CommandType commandType;
    public Vector3 targetPoint;
    public float radius;
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
    Defend,
    Deploy,
    Rotate,
    Bombard
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