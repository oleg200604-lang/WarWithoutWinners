using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

public class BattalionScr : MonoBehaviour
{
    public BatalionManagerScr batalionManager;
    public BattleManagerScr battleManager;
    public BattalionAttackSystemScr attackSystem;
    public BarScr bar;
    [Space]
    public string nameBattalion;
    public Personnel personnel;
    public Ammo ammo;
    public Battalion battalion;
    public int teamID;
    public int regimentredID = -1;
    public Company[] company;
    [Space]
    public Officer officer;
    public Command[] command = new Command[3];
    public bool isDefending;
    public Vector3 defendDirection = Vector3.right;
    public float orderDuration = 1f;
    public float footprintRadius = 0.6f;
    public bool isDeployed;
    public Vector3 deployDirection = Vector3.right;
    public float deployRange = 4f;

    public float deployConeAngle = 90f;
    public float bombardRadius = 1.5f;
    public GameObject isSelect;
    [Header("Туман війни")]
    [Tooltip(
        "Рендерери (спрайти/меші), які МАСКУЮТЬСЯ під туманом війни " +
        "(renderer.enabled = false — сам GameObject і всі його " +
        "компоненти лишаються повністю живими, батальйон НЕ зникає " +
        "з AllBattalions/AI). Якщо лишити порожнім — заповнюється " +
        "автоматично всіма Renderer у дочірніх об'єктах при Awake. " +
        "НІКОЛИ не використовуй тут GameObject.SetActive — саме " +
        "так туман випадково \"вимикав\" батальйон замість того, " +
        "щоб його промаскувати."
    )]
    public Renderer[] visualRenderers;
    [Tooltip("Колайдер, за яким батальйон обирається кліком. Вимикається (enabled=false, GameObject лишається активним) разом із visualRenderers, щоб прихованого ворога не можна було обрати.")]
    public Collider2D selectionCollider;
    [Header("Terrain sampling")]
    [SerializeField, Min(0.05f)]
    private float terrainSampleStep = 0.15f;
    private static readonly List<BattalionScr> AllBattalions = new List<BattalionScr>();
    private Battalion baseBattalion;
    private int basePersonnelMax;
    private Battalion restingBattalion;

    // За замовчуванням батальйон видимий (це важливо для дружніх
    // батальйонів, яких FogOfWarManagerScr ніколи не приховує —
    // вони мають лишатись видимими навіть якщо туман взагалі не
    // використовується в сцені).
    private bool fogVisible = true;
    public bool IsFogVisible => fogVisible;

    public static IReadOnlyList<BattalionScr> AllActive => AllBattalions;

    private void OnEnable()
    {
        AllBattalions.Add(this);
    }

    private void OnDisable()
    {
        AllBattalions.Remove(this);
    }

    private void Awake_FogSetup()
    {
        if (visualRenderers == null || visualRenderers.Length == 0)
            visualRenderers = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>
    /// Викликається FogOfWarManagerScr. МАСКУЄ (не вимикає) спрайт:
    /// вимикає лише Renderer.enabled на дочірніх рендерерах і
    /// selectionCollider.enabled — сам GameObject лишається active
    /// весь час, тому OnEnable/OnDisable (і, відповідно,
    /// AllBattalions) ніколи не спрацьовують через туман. Раніше тут
    /// стояв GameObject.SetActive(visible) на окремому полі
    /// visualRoot — якщо туди помилково призначали КОРІНЬ самого
    /// батальйона (а не дочірній об'єкт), туман фактично вимикав
    /// батальйон повністю, і той зникав з усіх розрахунків замість
    /// того, щоб просто стати невидимим. Renderer.enabled такої
    /// помилки в принципі не допускає.
    /// </summary>
    public void SetFogVisible(bool visible)
    {
        if (fogVisible == visible)
            return;

        fogVisible = visible;

        if (visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                    visualRenderers[i].enabled = visible;
            }
        }

        if (selectionCollider != null)
            selectionCollider.enabled = visible;

        if (!visible && isSelect != null)
            isSelect.SetActive(false);
    }

    /// <summary>
    /// Ефективна дальність виявлення (для тумана війни) з
    /// урахуванням місцевості, на якій зараз стоїть батальйон.
    /// </summary>
    public float GetEffectiveVisionRange()
    {
        if (TerrainManagerScr.Instance == null)
            return battalion.visionRange;

        return battalion.visionRange * TerrainManagerScr.Instance.GetVisionRangeMultiplier((Vector2)transform.position);
    }

    private void Awake()
    {
        Awake_FogSetup();

        command[0] = new MoveCommand();
        command[1] = new MoveCommand();
        command[2] = new MoveCommand();

        baseBattalion = battalion.Clone();
        basePersonnelMax = personnel.personnelMax;

        ammo.current = Mathf.Clamp(ammo.current, 0, ammo.max);

        RecalculateStats();
    }

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

        Vector3 origin = transform.position;

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
                origin += attack.direction * attack.moveDistance;
            }
        }

        origin.z = 0f;

        return origin;
    }

    public float GetEffectiveAttackRange(Vector3 origin)
    {
        if (TerrainManagerScr.Instance == null)
            return battalion.attackRange;

        return battalion.attackRange * TerrainManagerScr.Instance.GetAttackRangeMultiplier((Vector2)origin);
    }

    public float GetEffectiveAttackRange()
    {
        return GetEffectiveAttackRange(transform.position);
    }

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
            Vector3 samplePoint = from + direction * (segmentLength * (i + 0.5f));

            cost += segmentLength * TerrainManagerScr.Instance.GetMoveCost((Vector2)samplePoint);
        }

        return cost;
    }

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

    public float GetReachableDistance(Vector3 origin, Vector3 direction, float desiredDistance, float availableSpeed, float costMultiplier = 1f)
    {
        if (direction.sqrMagnitude < 0.001f)
            return 0f;

        if (desiredDistance <= 0f)
            return 0f;

        if (availableSpeed <= 0f)
            return 0f;

        direction.Normalize();

        float budget = availableSpeed * orderDuration;

        float low = 0f;
        float high = desiredDistance;

        for (int i = 0; i < 16; i++)
        {
            float middle = (low + high) * 0.5f;

            Vector3 point = origin + direction * middle;

            float cost = GetTerrainMoveCost(origin, point) * costMultiplier;

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

        return battalion.speed * orderDuration;
    }

    public bool SetMoveOrder(int slot, Vector3 pos)
    {
        if (command == null ||
            slot < 0 ||
            slot >= command.Length)
        {
            return false;
        }

        if (GetProjectedDeployedState(slot))
            return false;

        Vector3 origin =
            GetOrderOrigin(slot);

        pos.z = 0f;

        Vector3 direction =
            pos - origin;

        direction.z = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return false;

        float desiredDistance =
            direction.magnitude;

        direction.Normalize();

        float reachableDistance =
            GetReachableDistance(
                origin,
                direction,
                desiredDistance,
                battalion.speed,
                1f);

        if (reachableDistance <= 0.001f)
            return false;

        Vector3 finalPosition =
            origin +
            direction *
            reachableDistance;

        finalPosition.z = 0f;

        if (!IsPositionFree(
            finalPosition,
            footprintRadius,
            this))
        {
            return false;
        }

        ClearOrdersAfter(slot);

        isDefending = false;

        command[slot] =
            new MoveCommand
            {
                pos = finalPosition,
                commandType = CommandType.Move,
                isSet = true
            };

        return true;
    }

    public bool SetAttackOrder(int slot, Vector3 direction, float desiredMoveDistance)
    {
        if (command == null ||
            slot < 0 ||
            slot >= command.Length)
        {
            return false;
        }

        // На час розрахунку наказу атаки вмикаємо бонуси рот з умовою
        // "атака"/"атака і захист" — щоб дальність/швидкість вже
        // враховували їх ще на етапі постановки наказу.
        EnterAttackContext();

        try
        {
            if (GetProjectedDeployedState(slot))
                return false;

            if (direction.sqrMagnitude < 0.001f)
                return false;

            if (battalion.attackRange <= 0f)
                return false;

            Vector3 origin =
                GetOrderOrigin(slot);

            direction.z = 0f;
            direction.Normalize();

            float moveDistance =
                GetReachableDistance(
                    origin,
                    direction,
                    desiredMoveDistance,
                    battalion.speed,
                    battalion.attackMoveCostMultiplier);

            if (moveDistance <= 0.001f)
                return false;

            Vector3 targetPoint =
                origin +
                direction *
                moveDistance;

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

            command[slot] =
                new AttackOrder
                {
                    direction = direction,
                    moveDistance = moveDistance,

                    zoneRange =
                        GetEffectiveAttackRange(
                            targetPoint),

                    commandType = CommandType.Attack,
                    isSet = true
                };

            return true;
        }
        finally
        {
            ExitCombatContext();
        }
    }

    public bool SetDefendOrder(int slot, Vector3 direction)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        Vector3 finalDirection;
        float finalRange;

        // На час розрахунку наказу захисту вмикаємо бонуси рот з умовою
        // "захист"/"атака і захист".
        EnterDefendContext();

        try
        {
            bool projectedDeployed = GetProjectedDeployedState(slot);

            if (projectedDeployed)
            {
                finalDirection = GetProjectedDeployDirection(slot);

                finalRange = deployRange;
            }
            else
            {
                if (direction.sqrMagnitude < 0.001f ||
                    battalion.attackRange <= 0f)
                {
                    return false;
                }

                Vector3 origin = GetOrderOrigin(slot);

                finalDirection = direction.normalized;

                finalRange = GetEffectiveAttackRange(origin);
            }
        }
        finally
        {
            ExitCombatContext();
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
        bool projectedDeployed = isDeployed;

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
                projectedDeployed = deployOrder.deploy;
            }
        }

        return projectedDeployed;
    }
    public Vector3 GetProjectedDeployDirection(int slot)
    {
        Vector3 projectedDirection = deployDirection;

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
                    projectedDirection = deployOrder.direction.normalized;
                }
            }
            else if (command[i] is RotateOrder rotateOrder &&
                     rotateOrder.isSet)
            {
                if (rotateOrder.direction.sqrMagnitude > 0.001f)
                {
                    projectedDirection = rotateOrder.direction.normalized;
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

        bool currentlyDeployed = GetProjectedDeployedState(slot);

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

    public bool SetBombardOrder(int slot, Vector3 targetPoint)
    {
        if (command == null || slot < 0 || slot >= command.Length)
            return false;

        if (battalion.type != BattalionType.artillery)
            return false;

        if (!GetProjectedDeployedState(slot))
            return false;

        Vector3 origin = GetOrderOrigin(slot);

        targetPoint.z = 0f;

        Vector3 projectedDirection = GetProjectedDeployDirection(slot);

        Vector3 toTarget = targetPoint - origin;

        toTarget.z = 0f;

        float distance = toTarget.magnitude;

        if (distance > deployRange)
            return false;

        if (distance > 0.001f)
        {
            float angle = Vector3.Angle(projectedDirection, toTarget.normalized);

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

            // Прихований туманом війни ворог/нейтрал НЕ повинен
            // блокувати розміщення наказу. Інакше сама відмова
            // "точка зайнята" опосередковано видає гравцю, що там
            // хтось є, хоча візуально там порожньо (класичний витік
            // інформації через туман, тільки не через клік, а через
            // фізичну колізію). Власна команда і будь-хто, кого
            // спостерігач ЗАРАЗ бачить (CanSee), блокують як і раніше
            // — тут нічого нового не розкривається.
            if (self != null &&
                self.batalionManager != null &&
                other.teamID != self.teamID &&
                !self.batalionManager.CanSee(other))
            {
                continue;
            }

            float minDist = radius + other.footprintRadius;
            float distance = Vector3.Distance(other.transform.position, point);

            if (distance < minDist)
            {
                // Діагностика: друкуємо, ХТО саме заблокував точку —
                // ім'я об'єкта, повний шлях в ієрархії, teamID і
                // footprintRadius. Якщо блокувальник — не реальний
                // ворожий/дружній батальйон, а, наприклад, об'єкт
                // індикатора зони огляду (LineRenderer), це означає,
                // що на ньому випадково теж висить BattalionScr і він
                // зареєструвався в AllBattalions як окрема "фантомна"
                // одиниця точно в тій самій точці.
                Debug.LogWarning(
                    $"IsPositionFree: точку {point} заблокував " +
                    $"'{other.gameObject.name}' (шлях: {GetHierarchyPath(other.transform)}), " +
                    $"teamID={other.teamID}, footprintRadius={other.footprintRadius}, " +
                    $"позиція={other.transform.position}, дистанція={distance:F2}, " +
                    $"поріг={minDist:F2}",
                    other
                );

                return false;
            }
        }

        return true;
    }

    private static string GetHierarchyPath(Transform t)
    {
        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
    public float GetEffectiveSpeed(Vector3 origin)
    {
        float baseSpeed = battalion.speed;

        if (TerrainManagerScr.Instance == null)
            return baseSpeed;

        // GetMoveCost враховує ВСІ terrain-шари в точці, на відміну
        // від старого GetTypeAt (лише перший знайдений тайл).
        float multiplier = TerrainManagerScr.Instance.GetMoveCost((Vector2)origin);

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
        // Не можна навіть натиснути на батальйон, прихований
        // туманом війни — інакше клік сам по собі "видає" точну
        // позицію ворога (контур виділення/ім'я в консолі), а це
        // фактично дозволяє наводити накази "через" туман.
        if (!fogVisible)
            return;

        batalionManager.SelectBattalion(this);
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
            defendDirection = persistentDefend.direction.normalized;
        }
    }

    // Скільки шкоди сусід по ланцюгу "перебирає" на себе, коли атакують
    // цей батальйон. 1 сусід -> 25% йому, 75% основному. 2 сусіди -> по
    // 25% кожному, 50% основному. Немає сусідів (не в полку, або
    // фланговий без пари) -> 100% основному, як і раніше.
    private const float RegimentNeighborDamageShare = 0.25f;

    public void TakeDamage(float damage, float murder, float injury)
    {
        if (damage <= 0f)
            return;

        List<BattalionScr> neighbors = GetChainNeighbors();

        float perNeighborDamage = neighbors.Count > 0 ? damage * RegimentNeighborDamageShare : 0f;
        float selfDamage = damage - perNeighborDamage * neighbors.Count;

        ApplyDamage(selfDamage, murder, injury);

        for (int i = 0; i < neighbors.Count; i++)
        {
            if (neighbors[i] != null)
                neighbors[i].ApplyDamage(perNeighborDamage, murder, injury);
        }
    }

    // Фактичне застосування шкоди — без подальшого розподілу. Сусіди
    // отримують свою частку саме через цей метод (не через TakeDamage),
    // інакше шкода каскадно розповзлась би по всьому ланцюгу.
    private void ApplyDamage(float damage, float murder, float injury)
    {
        if (damage <= 0f)
            return;

        print(damage);

        personnel.Losses(murder, injury, damage, bar);
    }

    // Сусіди по ланцюгу цього батальйона в його полку (той самий порядок,
    // що обмежує рух — Regiment.battalions, максимум 2 сусіди: i-1, i+1).
    private List<BattalionScr> GetChainNeighbors()
    {
        List<BattalionScr> neighbors = new List<BattalionScr>();

        if (batalionManager == null ||
            batalionManager.regiments == null ||
            regimentredID < 0 ||
            regimentredID >= batalionManager.regiments.Count)
        {
            return neighbors;
        }

        Regiment regiment = batalionManager.regiments[regimentredID];

        if (regiment == null || regiment.battalions == null)
            return neighbors;

        int index = regiment.battalions.IndexOf(this);

        if (index < 0)
            return neighbors;

        if (index > 0 && regiment.battalions[index - 1] != null)
            neighbors.Add(regiment.battalions[index - 1]);

        if (index < regiment.battalions.Count - 1 && regiment.battalions[index + 1] != null)
            neighbors.Add(regiment.battalions[index + 1]);

        return neighbors;
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

        bar.SetOrganization(personnel.organization, personnel.organizationMax);

        bar.SetcombatCapable(personnel.combatCapable, personnel.combatCapableNo, personnel.personnelMax);

        bar.SetAmmo(ammo.current, ammo.max);
    }

    private IEnumerator ExecuteOrders()
    {
        for (int i = 0; i < command.Length; i++)
        {
            if (command[i] is MoveCommand move &&
                move.isSet)
            {
                Vector3 start = transform.position;

                Vector3 target = new Vector3(move.pos.x, move.pos.y, 0f);

                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;

                    transform.position = Vector3.Lerp(start, target, t / orderDuration);

                    yield return null;
                }

                transform.position = target;

                print("Move: " + target);
            }
            else if (command[i] is AttackOrder attack &&
                     attack.isSet)
            {
                // Бонуси рот "лише атака"/"атака і захист" діють на весь
                // час виконання наказу атаки (рух + постріл).
                EnterAttackContext();

                if (!ammo.HasEnough(battalion.ammoCostPerAction))
                {
                    print(nameBattalion + ": немає боєприпасів для атаки — наказ не виконано.");
                    ExitCombatContext();
                    yield return new WaitForSeconds(orderDuration);
                    continue;
                }

                Vector3 start = transform.position;

                Vector3 target = start + attack.direction * attack.moveDistance;

                if (attackSystem == null)
                {
                    Debug.LogWarning(nameBattalion + ": attackSystem не призначено.", this);
                }

                bool hasHit = false;

                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;

                    transform.position = Vector3.Lerp(start, target, t / orderDuration);

                    if (!hasHit &&
                        attackSystem != null)
                    {
                        BattalionScr hitTarget = attackSystem.FindTarget(this, attack.direction, attack.zoneRange);

                        if (hitTarget != null)
                        {
                            ammo.TrySpend(battalion.ammoCostPerAction);

                            float damage = ComputeAttackDamage();

                            hitTarget.TakeDamage(damage, battalion.murder, battalion.injury);

                            print(nameBattalion + ": атака влучила по " + hitTarget.nameBattalion);

                            hasHit = true;
                        }
                    }

                    yield return null;
                }

                transform.position = target;

                if (attackSystem != null &&
                    !hasHit)
                {
                    print(nameBattalion + ": атака нікого не зачепила");
                }

                ExitCombatContext();
            }
            else if (command[i] is DefendOrder defend &&
                     defend.isSet)
            {
                // Бонуси рот "лише захист"/"атака і захист" діють на весь
                // час, поки батальйон стоїть у захисті цього ходу.
                EnterDefendContext();

                isDefending = true;
                defendDirection = defend.direction.normalized;

                bool hasFired = false;

                float t = 0f;

                while (t < orderDuration)
                {
                    t += Time.deltaTime;

                    if (!hasFired &&
                        attackSystem != null)
                    {
                        BattalionScr hitTarget = attackSystem.FindTarget(this, defendDirection, defend.range);

                        if (hitTarget != null)
                        {
                            if (!ammo.TrySpend(battalion.ammoCostPerAction))
                            {
                                print(nameBattalion + ": немає боєприпасів для пострілу в захисті.");
                            }
                            else
                            {
                                float damage = ComputeAttackDamage();

                                hitTarget.TakeDamage(damage, battalion.murder, battalion.injury);

                                print(nameBattalion + ": захист влучив по " + hitTarget.nameBattalion);
                            }

                            hasFired = true;
                        }
                    }

                    yield return null;
                }

                if (!hasFired)
                {
                    print(nameBattalion + ": захист нікого не побачив");
                }

                // НЕ видаляємо DefendOrder.
                // Він залишається активним між ходами.

                ExitCombatContext();
            }
            else if (command[i] is DeployOrder deployOrder &&
                     deployOrder.isSet)
            {
                isDeployed = deployOrder.deploy;

                if (deployOrder.deploy)
                {
                    deployDirection = deployOrder.direction.normalized;
                }

                print(nameBattalion + (isDeployed ? ": розклалась, напрямок " + deployDirection : ": згорнулась"));

                yield return
                    new WaitForSeconds(orderDuration);
            }
            else if (command[i] is RotateOrder rotateOrder &&
                     rotateOrder.isSet)
            {
                deployDirection = rotateOrder.direction.normalized;

                print(nameBattalion + ": змінила напрямок наведення на " + deployDirection);

                yield return
                    new WaitForSeconds(orderDuration);
            }
            else if (command[i] is BombardOrder bombardOrder &&
                     bombardOrder.isSet)
            {
                // Обстріл — наступальна дія, тож рахуємо його як "атаку"
                // для бонусів рот з умовою AttackOnly/AttackAndDefend.
                EnterAttackContext();

                if (!ammo.HasEnough(battalion.ammoCostPerAction))
                {
                    print(nameBattalion + ": немає боєприпасів для обстрілу — наказ не виконано.");
                }
                else if (attackSystem == null)
                {
                    Debug.LogWarning(nameBattalion + ": attackSystem не призначено — обстріл не завдає шкоди.", this);
                }
                else
                {
                    List<BattalionScr> hitTargets = attackSystem.FindTargetsInRadius(bombardOrder.targetPoint, bombardOrder.radius, this);

                    if (hitTargets.Count > 0)
                    {
                        ammo.TrySpend(battalion.ammoCostPerAction);

                        float damage = ComputeAttackDamage();

                        foreach (
                            BattalionScr hitTarget
                            in hitTargets)
                        {
                            hitTarget.TakeDamage(damage, battalion.murder, battalion.injury);

                            print(nameBattalion + ": обстріл влучив по " + hitTarget.nameBattalion);
                        }
                    }
                    else
                    {
                        print(nameBattalion + ": обстріл нікого не зачепив");
                    }
                }

                ExitCombatContext();

                yield return
                    new WaitForSeconds(orderDuration);
            }
            else
            {
                yield return
                    new WaitForSeconds(orderDuration);
            }
        }

        ClearAllOrders();
    }

    // Скільки бракує особового складу до максимуму (боєздатні + небоєздатні < максимум).
    public int GetMissingPersonnel()
    {
        int missing = personnel.personnelMax - (personnel.combatCapable + personnel.combatCapableNo);
        return missing > 0 ? missing : 0;
    }

    // Додає amount новобранців (боєздатних) і "розмазує" середній досвід
    // батальйону: новобранці мають досвід 0, тож середнє зважується
    // старою і новою кількістю особового складу. Повертає фактично
    // додану кількість (обрізану до того, скільки реально бракувало).
    public int ReinforcePersonnel(int amount)
    {
        int missing = GetMissingPersonnel();

        int actualAmount = Mathf.Min(amount, missing);

        if (actualAmount <= 0)
            return 0;

        int oldTotal = personnel.combatCapable + personnel.combatCapableNo;
        int newTotal = oldTotal + actualAmount;

        if (newTotal > 0)
        {
            // Новобранці рахуються з досвідом 0, тому загальна сума
            // досвіду не змінюється — лише "розмазується" на більшу
            // кількість людей.
            personnel.experience = personnel.experience * oldTotal / newTotal;
        }

        personnel.combatCapable += actualAmount;

        return actualAmount;
    }

    public bool AddCompany(int selectCompany, CompanyType compan)
    {
        if (company == null ||
            selectCompany < 0 ||
            selectCompany >= company.Length)
        {
            Debug.LogWarning(nameBattalion + ": невірний слот роти " + selectCompany);
            return false;
        }

        if (CompanyDatabaseScr.Instance == null)
        {
            Debug.LogWarning(nameBattalion + ": CompanyDatabaseScr не знайдено на сцені — бонуси роти не застосовано.");
            return false;
        }

        if (compan != CompanyType.none &&
            !CompanyDatabaseScr.Instance.TryGetDefinition(compan, out _))
        {
            Debug.LogWarning(nameBattalion + ": для типу роти " + compan + " немає визначення в CompanyDatabaseScr.");
            return false;
        }

        company[selectCompany] = new Company { company = compan };

        RecalculateStats();

        return true;
    }

    public bool RemoveCompany(int selectCompany)
    {
        if (company == null ||
            selectCompany < 0 ||
            selectCompany >= company.Length)
        {
            return false;
        }

        company[selectCompany] = new Company { company = CompanyType.none };

        RecalculateStats();

        return true;
    }

    // Перераховує battalion/personnel.personnelMax як (базові значення) + (сума бонусів усіх активних рот).
    // Викликати щоразу після зміни складу рот батальйону.
    // personnelMaxBonus діє завжди; бойові statBonus зберігаються у
    // restingBattalion лише ті, чия умова — Always (решта додаються
    // тимчасово через EnterAttackContext/EnterDefendContext).
    public void RecalculateStats()
    {
        int personnelMax = basePersonnelMax;

        if (company != null &&
            CompanyDatabaseScr.Instance != null)
        {
            for (int i = 0; i < company.Length; i++)
            {
                Company slot = company[i];

                if (slot == null ||
                    slot.company == CompanyType.none)
                {
                    continue;
                }

                if (!CompanyDatabaseScr.Instance.TryGetDefinition(slot.company, out CompanyDefinition definition))
                {
                    continue;
                }

                personnelMax += definition.personnelMaxBonus;
            }
        }

        personnel.personnelMax = personnelMax;

        restingBattalion = BuildBattalion(false, false);
        battalion = restingBattalion.Clone();
    }

    // Складає ефективні бойові стати: базові + бонуси рот, чия умова
    // дозволяє застосування у поточному контексті.
    // includeAttack/includeDefend — чи триває зараз атака/обстріл чи захист.
    private Battalion BuildBattalion(bool includeAttack, bool includeDefend)
    {
        Battalion result = baseBattalion.Clone();

        if (company == null ||
            CompanyDatabaseScr.Instance == null)
        {
            return result;
        }

        for (int i = 0; i < company.Length; i++)
        {
            Company slot = company[i];

            if (slot == null ||
                slot.company == CompanyType.none)
            {
                continue;
            }

            if (!CompanyDatabaseScr.Instance.TryGetDefinition(slot.company, out CompanyDefinition definition) ||
                definition.statBonus == null)
            {
                continue;
            }

            bool applies;

            switch (definition.condition)
            {
                case CompanyBonusCondition.AttackOnly:
                    applies = includeAttack;
                    break;
                case CompanyBonusCondition.DefendOnly:
                    applies = includeDefend;
                    break;
                case CompanyBonusCondition.AttackAndDefend:
                    applies = includeAttack || includeDefend;
                    break;
                default:
                    applies = true; // Always
                    break;
            }

            if (!applies)
                continue;

            result.attackRange += definition.statBonus.attackRange;
            result.attackMoveCostMultiplier += definition.statBonus.attackMoveCostMultiplier;
            result.attackConeAngle += definition.statBonus.attackConeAngle;
            result.damage += definition.statBonus.damage;
            result.murder += definition.statBonus.murder;
            result.injury += definition.statBonus.injury;
            result.speed += definition.statBonus.speed;
        }

        return result;
    }

    // Тимчасово вмикає бонуси рот з умовою "лише атака"/"атака і захист".
    // Викликати перед розрахунками, пов'язаними з атакою/обстрілом,
    // і обов'язково повернутись у стан спокою через ExitCombatContext().
    public void EnterAttackContext()
    {
        battalion = BuildBattalion(true, false);
    }

    // Тимчасово вмикає бонуси рот з умовою "лише захист"/"атака і захист".
    public void EnterDefendContext()
    {
        battalion = BuildBattalion(false, true);
    }

    // Повертає battalion у "стан спокою" (базові стати + бонуси Always).
    public void ExitCombatContext()
    {
        battalion = restingBattalion.Clone();
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
    public int organization;
    public int organizationMax;

    [Tooltip("Середній досвід особового складу батальйону (0..100). При поповненні розмазується на нове поповнення (у новобранців досвід = 0).")]
    [Range(0f, 100f)]
    public float experience;
    public void Losses(float deadRatio, float earlyRatio, float damage, BarScr bar)
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


[System.Serializable]
public class Ammo
{
    public int current;
    public int max;

    public bool HasEnough(int cost)
    {
        return current >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (cost <= 0)
            return true;

        if (current < cost)
            return false;

        current -= cost;
        return true;
    }

    public void Add(int amount)
    {
        current = Mathf.Clamp(current + amount, 0, max);
    }
}

public enum CommandType
{
    None, Move, Attack, Defend, Deploy, Rotate, Bombard
}

public interface Command
{

}

[System.Serializable]
public class Battalion
{
    public BattalionType type;

    [Tooltip("Фіксована дальність атаки.")]
    public float attackRange = 2.5f;

    [Tooltip("У скільки разів рух під час атаки дорожчий за звичайний Move.")]
    public float attackMoveCostMultiplier = 2f;

    [Tooltip("Кут сектора атаки цього батальйону.")]
    [Range(1f, 360f)]
    public float attackConeAngle = 90f;

    public float damage;
    public float murder;
    public float injury;
    public float speed;

    [Header("Видимість (туман війни)")]
    [Tooltip("Базова дальність, на якій батальйон розсіює туман війни (бачить ворогів).")]
    public float visionRange = 6f;

    [Header("Ресурси")]
    [Tooltip("Скільки боєприпасів витрачається на одну активну дію (постріл при атаці/захисті/обстрілі).")]
    public int ammoCostPerAction = 10;
    [Tooltip("Скільки командного ресурсу коштує ОДИН наказ цьому батальйону. Полк рахується як один батальйон — береться це значення з першого батальйона полку.")]
    public int commandCost = 1;

    public Battalion Clone()
    {
        return new Battalion
        {
            type = type,
            attackRange = attackRange,
            attackMoveCostMultiplier = attackMoveCostMultiplier,
            attackConeAngle = attackConeAngle,
            damage = damage,
            murder = murder,
            injury = injury,
            speed = speed,
            visionRange = visionRange,
            ammoCostPerAction = ammoCostPerAction,
            commandCost = commandCost
        };
    }
}

[System.Serializable]
public class Company
{
    public CompanyType company;
}

public enum BattalionType
{
    none, infantry, artillery, cavalry, mechanically
}

public enum CompanyType
{
    none, machineGun, medical, cannon, flamethrower
}

public enum EffectType
{
    none, suppressed, battle, panic
}

[System.Serializable]
public class ActiveEffect
{
    public EffectType type;
    public int remainingTurns;
}