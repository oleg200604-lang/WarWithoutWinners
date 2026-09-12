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
    public Officer officer;
    public Officer officerRegiment;
    public Proficiency proficiency;
    public float baseRestoration;
    [Space]
    public Command[] command = new Command[3];

    public bool isDefending;
    public Vector3 defendDirection = Vector3.right;
    public float orderDuration = 1f;
    [Tooltip("Резервний радіус \"footprint\" для колізій (рух/ближній бій), якщо selectionCollider не заданий. Коли selectionCollider є — радіус береться з нього (EffectiveFootprintRadius), це поле ігнорується.")]
    public float footprintRadius = 0.6f;
    public float EffectiveFootprintRadius
    {
        get
        {
            if (selectionCollider != null)
            {
                Bounds b = selectionCollider.bounds;
                return (b.extents.x + b.extents.y) * 0.5f;
            }

            return footprintRadius;
        }
    }

    [Tooltip("Додатковий запас радіусу ТІЛЬКИ для виявлення ближнього бою (щоб зіткнення реагувало трохи раніше, до фактичного накладання спрайтів). На IsPositionFree (розміщення наказів Move/Attack) НЕ впливає — командування лишається таким самим, як і з реальним розміром колайдера.")]
    public float meleeCollisionPadding = 0.15f;
    public float EffectiveMeleeRadius => EffectiveFootprintRadius + meleeCollisionPadding;
    public bool isDeployed;
    public Vector3 deployDirection = Vector3.right;
    public float deployRange = 4f;

    public float deployConeAngle = 90f;
    public float bombardRadius = 1.5f;
    public GameObject isSelect;
    [Header("Туман війни")]
    [Tooltip("Рендерери (спрайти/меші), які МАСКУЮТЬСЯ під туманом війни " + "(renderer.enabled = false — сам GameObject і всі його " + "компоненти лишаються повністю живими, батальйон НЕ зникає " + "з AllBattalions/AI). Якщо лишити порожнім — заповнюється " + "автоматично всіма Renderer у дочірніх об'єктах при Awake. " + "НІКОЛИ не використовуй тут GameObject.SetActive — саме " + "так туман випадково \"вимикав\" батальйон замість того, " + "щоб його промаскувати.")]
    public Renderer[] visualRenderers;
    [Tooltip("Колайдер, за яким батальйон обирається кліком. Вимикається (enabled=false, GameObject лишається активним) разом із visualRenderers, щоб прихованого ворога не можна було обрати.")]
    public Collider2D selectionCollider;
    [Header("Terrain sampling")]
    [SerializeField, Min(0.05f)]
    private float terrainSampleStep = 0.15f;
    private static readonly List<BattalionScr> AllBattalions = new List<BattalionScr>();
    private Battalion baseBattalion;
    private int basePersonnelMax;
    private float baseOrganizationMax;
    private Battalion restingBattalion;
    private bool fogVisible = true;
    public bool IsFogVisible => fogVisible;
    public static IReadOnlyList<BattalionScr> AllActive => AllBattalions;
    public int TeamID => batalionManager != null ? batalionManager.teamID : teamID;

    private void SyncManagerReference()
    {
        if (batalionManager == null)
            batalionManager = GetComponentInParent<BatalionManagerScr>();

        if (batalionManager != null)
            teamID = batalionManager.teamID;
    }

    public bool IsEnemy(BattalionScr other)
    {
        if (other == null || other == this)
            return false;

        if (batalionManager != null && other.batalionManager != null)
            return batalionManager.IsEnemyTo(other.batalionManager);

        return TeamID != other.TeamID;
    }

    public bool IsAlly(BattalionScr other)
    {
        if (other == null)
            return false;

        if (batalionManager != null && other.batalionManager != null)
            return batalionManager.IsAlly(other.batalionManager.teamID);

        return TeamID == other.TeamID;
    }

    private void OnEnable()
    {
        if (!AllBattalions.Contains(this))
            AllBattalions.Add(this);

        SyncManagerReference();
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

    public float GetEffectiveVisionRange()
    {
        if (TerrainManagerScr.Instance == null)
            return battalion.visionRange;

        return battalion.visionRange * TerrainManagerScr.Instance.GetVisionRangeMultiplier((Vector2)transform.position);
    }

    private void Awake()
    {
        Awake_FogSetup();
        SyncManagerReference();

        command[0] = new MoveCommand();
        command[1] = new MoveCommand();
        command[2] = new MoveCommand();

        if (battalion != null)
            baseBattalion = battalion.Clone();

        if (personnel != null)
        {
            basePersonnelMax = personnel.personnelMax;
            baseOrganizationMax = personnel.organizationMax;
        }

        if (ammo != null)
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
            EffectiveFootprintRadius,
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
                EffectiveFootprintRadius,
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

            if (self != null &&
                self.batalionManager != null &&
                other.TeamID != self.TeamID &&
                !self.batalionManager.CanSee(other))
            {
                continue;
            }

            float minDist = radius + other.EffectiveFootprintRadius;
            float distance = Vector3.Distance(other.transform.position, point);

            if (distance < minDist)
            {
                Debug.LogWarning(
                    $"IsPositionFree: точку {point} заблокував " +
                    $"'{other.gameObject.name}' (шлях: {GetHierarchyPath(other.transform)}), " +
                    $"teamID={other.TeamID}, footprintRadius={other.EffectiveFootprintRadius}, " +
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

    private void ApplyDamage(float damage, float murder, float injury)
    {
        if (damage <= 0f)
            return;

        // Бонус "Захист" зменшує реально отриману шкоду: damage / defenseMultiplier.
        float defenseMultiplier = (battalion != null && battalion.defenseMultiplier > 0f) ? battalion.defenseMultiplier : 1f;
        float mitigatedDamage = damage / defenseMultiplier;

        print(mitigatedDamage);

        int losses = personnel.LossesPersonnel(murder, injury, mitigatedDamage);

        // Втрата організації: шкода/10 + втрати/10.
        personnel.LossesOrganization(mitigatedDamage, losses);
    }

    // Сусіди по ланцюгу цього батальйона в його полку (той самий порядок,
    // що обмежує рух — Regiment.battalions, максимум 2 сусіди: i-1, i+1).
    private List<BattalionScr> GetChainNeighbors()
    {
        List<BattalionScr> neighbors = new List<BattalionScr>();

        if (batalionManager == null || batalionManager.regiments == null)
            return neighbors;

        Regiment regiment = null;

        // regimentredID залишаємо лише як кеш/сумісність зі старими даними.
        // Фактичний полк визначається за присутністю цього батальйону в списку.
        for (int i = 0; i < batalionManager.regiments.Count; i++)
        {
            Regiment candidate = batalionManager.regiments[i];
            if (candidate != null && candidate.battalions != null && candidate.battalions.Contains(this))
            {
                regiment = candidate;
                regimentredID = i;
                break;
            }
        }

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
    private BattalionScr FindMeleeCollision()
    {
        return FindMeleeCollisionAt(transform.position);
    }

    private BattalionScr FindMeleeCollisionAt(Vector3 selfPos)
    {
        if (batalionManager == null)
            return null;

        IReadOnlyList<BattalionScr> all = AllActive;

        for (int i = 0; i < all.Count; i++)
        {
            BattalionScr other = all[i];

            if (other == null || other == this)
                continue;

            if (!IsEnemy(other))
                continue;

            Vector3 delta = other.transform.position - selfPos;
            delta.z = 0f;

            float combinedRadius = EffectiveMeleeRadius + other.EffectiveMeleeRadius;

            if (delta.magnitude <= combinedRadius)
                return other;
        }

        return null;
    }

    private Vector3 ClampToContactPoint(Vector3 from, Vector3 to, BattalionScr enemy)
    {
        Vector3 enemyPos = enemy.transform.position;
        float combinedRadius = EffectiveMeleeRadius + enemy.EffectiveMeleeRadius;

        Vector3 d = to - from;
        Vector3 f = from - enemyPos;
        d.z = 0f;
        f.z = 0f;

        float a = Vector3.Dot(d, d);

        if (a <= Mathf.Epsilon)
            return from;

        float b = 2f * Vector3.Dot(f, d);
        float c = Vector3.Dot(f, f) - combinedRadius * combinedRadius;

        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0f)
            return from;

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float s1 = (-b - sqrtDiscriminant) / (2f * a);
        float s2 = (-b + sqrtDiscriminant) / (2f * a);

        float s = Mathf.Min(s1, s2);

        if (s < 0f)
            s = Mathf.Max(s1, s2);

        s = Mathf.Clamp01(s);

        return from + d * s;
    }

    private void ResolveMeleeCollision(BattalionScr enemy)
    {
        if (enemy == null)
            return;

        print(nameBattalion + ": зіткнення в ближньому бою з " + enemy.nameBattalion + "!");

        if (battalion.meleeAttack > 0)
        {
            enemy.TakeDamage(battalion.meleeAttack, battalion.murder, battalion.injury);
        }

        if (enemy.battalion.meleeAttack > 0)
        {
            TakeDamage(enemy.battalion.meleeAttack, enemy.battalion.murder, enemy.battalion.injury);
        }
    }

    private float ComputeAttackDamage()
    {
        return battalion.damage * (float)(personnel.personnelMax / (personnel.combatCapable + (personnel.combatCapableNo / 2))) * (personnel.organizationMax / personnel.organization);
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

                    Vector3 candidate = Vector3.Lerp(start, target, t / orderDuration);

                    BattalionScr meleeEnemy = FindMeleeCollisionAt(candidate);

                    if (meleeEnemy != null)
                    {
                        // Не застосовуємо candidate (він вже може бути
                        // ВСЕРЕДИНІ ворога) — зупиняємось рівно в точці
                        // дотику, порахованій від фактичної поточної
                        // позиції (transform.position), щоб не було
                        // візуального накладання батальйонів.
                        transform.position = ClampToContactPoint(
                            transform.position,
                            candidate,
                            meleeEnemy);

                        ResolveMeleeCollision(meleeEnemy);

                        // Зіткнення зупиняє виконання ВСІХ подальших
                        // наказів цього ходу — це не окремий наказ,
                        // а перерваний Move.
                        ClearAllOrders();

                        yield break;
                    }

                    transform.position = candidate;

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

    public void SelectOfficer(Officer officers)
    {
        if (officers.officetType == baseBattalion.type)
        {
            if (officers.isSelect == false)
            {
                officer = officers;
                officer.isSelect = true;

                RecalculateStats();
            }
            else
            {
                if (officer.rank == Rank.General)
                {

                }
            }

        }
    }

    // Знімає особистого офіцера з батальйону (звільняє його для повторного призначення)
    // і перераховує статистику батальйону без його бонусів.
    public void UnassignOfficer()
    {
        if (officer == null)
            return;

        officer.isSelect = false;
        officer = null;

        RecalculateStats();
    }

    // Знімає офіцера полку з цього конкретного батальйону (наприклад, при виході
    // батальйону з полку). isSelect офіцера НЕ скидається тут — цим керує сам
    // Regiment, бо один офіцер полку належить одразу кільком батальйонам.
    public void ClearOfficerRegiment()
    {
        if (officerRegiment == null)
            return;

        officerRegiment = null;

        RecalculateStats();
    }

    public int GetMissingPersonnel()
    {
        int missing = personnel.personnelMax - (personnel.combatCapable + personnel.combatCapableNo);
        return missing > 0 ? missing : 0;
    }

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

    // Сумарний відсотковий бонус вміння від ОБОХ офіцерів батальйону (особистий + полковий),
    // кожен вже з поправкою на ефективність свого звання.
    private float GetOfficerBonusPercent(System.Func<Officer, float> bonusSelector)
    {
        float total = 0f;

        if (officer != null)
            total += bonusSelector(officer);

        if (officerRegiment != null)
            total += bonusSelector(officerRegiment);

        return total;
    }

    // Аналог GetOfficerBonusPercent, але для ефектів-множників (superior/mutualRespect тощо),
    // які комбінуються між собою множенням, а не додаванням.
    private float GetOfficerMultiplier(System.Func<Officer, float> multiplierSelector)
    {
        float total = 1f;

        if (officer != null)
            total *= multiplierSelector(officer);

        if (officerRegiment != null)
            total *= multiplierSelector(officerRegiment);

        return total;
    }

    // Особистий офіцер (Майор) має пріоритет над офіцером полку — саме він
    // безпосередньо командує цим батальйоном і "дає" накази.
    private Officer GetCommandingOfficer()
    {
        return officer != null ? officer : officerRegiment;
    }

    // Вартість наступного наказу з урахуванням особливостей командувача
    // (stubborn/sycophantic/superior/mutualRespect), без витрачання "черги округлення".
    public int PeekEffectiveCommandCost()
    {
        int baseCost = battalion != null ? battalion.commandCost : 0;
        Officer commanding = GetCommandingOfficer();

        return commanding != null
            ? commanding.PeekCommandCost(baseCost)
            : baseCost;
    }

    // Фактично списує вартість наказу. Викликати РІВНО ОДИН РАЗ на реально відданий наказ.
    public int ConsumeEffectiveCommandCost()
    {
        int baseCost = battalion != null ? battalion.commandCost : 0;
        Officer commanding = GetCommandingOfficer();

        return commanding != null
            ? commanding.ConsumeCommandCost(baseCost)
            : baseCost;
    }

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

        // Вміння "Організація": +10% максимальної організації за рівень (з ефективністю звання).
        float organizationBonusPercent = GetOfficerBonusPercent(o => o.GetOrganizationBonusPercent());

        // superior (x0.75) / mutualRespect (x1.25) — пряма зміна максимальної організації.
        float organizationMaxMultiplier = GetOfficerMultiplier(o => o.GetOrganizationMaxMultiplier());

        personnel.organizationMax = baseOrganizationMax * (1f + organizationBonusPercent) * organizationMaxMultiplier;

        if (personnel.organization > personnel.organizationMax)
            personnel.organization = personnel.organizationMax;

        restingBattalion = BuildBattalion(false, false);
        battalion = restingBattalion.Clone();
    }

    private Battalion BuildBattalion(bool includeAttack, bool includeDefend)
    {
        Battalion result = baseBattalion.Clone();

        // Пасивні бонуси від вмінь офіцерів (Майор/Підполковник/Полковник/Генерал),
        // ефективність вже врахована в GetXBonusPercent().
        float tacticsBonusPercent = GetOfficerBonusPercent(o => o.GetTacticsBonusPercent());
        float attackBonusPercent = GetOfficerBonusPercent(o => o.GetAttackBonusPercent());
        float defenseBonusPercent = GetOfficerBonusPercent(o => o.GetDefenseBonusPercent());

        result.speed *= 1f + tacticsBonusPercent;
        result.damage *= 1f + attackBonusPercent;
        result.defenseMultiplier = 1f + defenseBonusPercent;

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
    public void EnterAttackContext()
    {
        battalion = BuildBattalion(true, false);
    }

    public void EnterDefendContext()
    {
        battalion = BuildBattalion(false, true);
    }

    public void ExitCombatContext()
    {
        battalion = restingBattalion.Clone();
    }

    public void RecoveryOrganization(float restoration)
    {
        if (personnel.organization < personnel.organizationMax)
        {
            // charismatic (x2) / strict (x0.5) — швидкість регенерації організації.
            float regenMultiplier = GetOfficerMultiplier(o => o.GetOrganizationRegenMultiplier());

            personnel.organization += (int)(restoration * regenMultiplier);

            if (personnel.organization > personnel.organizationMax)
                personnel.organization = personnel.organizationMax;
        }
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
    public Vector3 direction;
    public float range;
    public bool isSet;
}

[System.Serializable]
public class DeployOrder : Command
{
    public CommandType commandType;
    public bool deploy;
    public Vector3 direction;
    public bool isSet;
}

[System.Serializable]
public class RotateOrder : Command
{
    public CommandType commandType;
    public Vector3 direction;
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
    public float organization;
    public float organizationMax;

    [Range(0f, 1000f)]
    public float experience;
    // Повертає фактичну кількість втраченого особового складу (для розрахунку втрати організації).
    public int LossesPersonnel(float deadRatio, float earlyRatio, float damage)
    {
        if (damage <= 0)
            return 0;

        int damageAmount = (int)damage;

        if (damageAmount <= 0)
            return 0;


        if (combatCapable <= 0)
        {
            int killedEarly = System.Math.Min(damageAmount, combatCapableNo);
            combatCapableNo -= killedEarly;
            return killedEarly;
        }


        int actualDamage = System.Math.Min(damageAmount, combatCapable);

        float ratioSum = deadRatio + earlyRatio;

        if (ratioSum <= 0)
            return 0;

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

        return actualDamage;
    }

    // Втрата організації = шкода/10 + втрати(особового складу)/10.
    public void LossesOrganization(float damage, float losses)
    {
        float reduction = damage / 10f + losses / 10f;

        organization -= reduction;

        if (organization < 0f)
            organization = 0f;
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

    [Tooltip("Шкода ближнього бою — застосовується автоматично, коли батальйон під час виконання наказу зіштовхується з ворожим батальйоном (не є окремим наказом).")]
    public int meleeAttack;

    [Header("Видимість (туман війни)")]
    [Tooltip("Базова дальність, на якій батальйон розсіює туман війни (бачить ворогів).")]
    public float visionRange = 6f;

    [Header("Ресурси")]
    [Tooltip("Скільки боєприпасів витрачається на одну активну дію (постріл при атаці/захисті/обстрілі).")]
    public int ammoCostPerAction = 10;
    [Tooltip("Скільки командного ресурсу коштує ОДИН наказ цьому батальйону. Полк рахується як один батальйон — береться це значення з першого батальйона полку.")]
    public int commandCost = 1;

    [Tooltip("Множник пасивного бонусу \"Захист\" від офіцера: 1 = без бонусу, 1.25 = -20% отримуваної шкоди тощо. Застосовується як damage / defenseMultiplier.")]
    public float defenseMultiplier = 1f;

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
            meleeAttack = meleeAttack,
            visionRange = visionRange,
            ammoCostPerAction = ammoCostPerAction,
            commandCost = commandCost,
            defenseMultiplier = defenseMultiplier
        };
    }
}
public enum Proficiency
{
    recruits, trained, experienced, veterans, elite
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