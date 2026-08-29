using UnityEngine;

/// <summary>
/// Візуалізація наказів одного батальйону.
///
/// НЕ містить ігрової логіки.
/// Використовує BattalionScr для визначення:
/// - початкової точки наказу;
/// - доступної дистанції;
/// - впливу місцевості;
/// - дальності атаки/захисту.
///
/// Показується тільки для вибраного батальйону.
/// </summary>
[RequireComponent(typeof(BattalionScr))]
public class BattalionVisualsScr : MonoBehaviour
{
    [Header("Посилання")]
    public BattalionScr battalion;
    public BatalionManagerScr batalionManager;

    [Header("Мітки наказу руху")]
    public GameObject orderMarkerPrefab;
    private readonly GameObject[] orderMarkers = new GameObject[3];

    [Header("Мітки наказу атаки")]
    public GameObject attackMarkerPrefab;
    private readonly GameObject[] attackMarkers = new GameObject[3];

    [Header("Мітки наказу захисту")]
    public GameObject defendMarkerPrefab;
    private readonly GameObject[] defendMarkers = new GameObject[3];

    [Header("Стрілки між точками наказів")]
    [Tooltip(
        "Префаб повинен містити LineRenderer. " +
        "LineRenderer працює у світових координатах."
    )]
    public GameObject arrowPrefab;

    private readonly GameObject[] orderArrows = new GameObject[3];

    private void Reset()
    {
        battalion = GetComponent<BattalionScr>();
    }

    private void Awake()
    {
        if (battalion == null)
            battalion = GetComponent<BattalionScr>();
    }

    private void Update()
    {
        if (battalion == null)
            return;

        if (batalionManager == null)
            return;

        bool isSelected =
            batalionManager.selectBattalion == battalion ||
            (batalionManager.selectRegiment != null &&
             batalionManager.selectRegiment.battalions.Contains(battalion));

        if (!isSelected)
        {
            HideAllVisuals();
            return;
        }

        UpdateOrders();
    }

    // =========================================================
    // ОСНОВНЕ ОНОВЛЕННЯ
    // =========================================================

    private void UpdateOrders()
    {
        if (battalion.command == null)
        {
            HideAllVisuals();
            return;
        }

        int count = Mathf.Min(battalion.command.Length, 3);

        for (int i = 0; i < 3; i++)
        {
            if (i >= count)
            {
                HideSlot(i);
                continue;
            }

            UpdateSlot(i);
        }
    }

    private void UpdateSlot(int slot)
    {
        Command command =
            battalion.command[slot];

        bool hasMove =
            command is MoveCommand move &&
            move.isSet;

        bool hasAttack =
            command is AttackOrder attack &&
            attack.isSet;

        bool hasDefend =
            command is DefendOrder defend &&
            defend.isSet;

        // Поки гравець ще ЦІЛИТЬСЯ (не підтвердив клацанням) наказом
        // Move/Attack для цього слота, фантомну мітку/стрілку веде
        // RangeIndicatorScr (ShowPhantomMove/ShowPhantomAttack) — тут
        // її ховати не можна, інакше вона миготітиме щокадру.
        bool isCurrentSlot =
            batalionManager != null &&
            batalionManager.selectBattalion == battalion &&
            batalionManager.CommandDuty == slot;

        bool isAimingMove =
            isCurrentSlot &&
            batalionManager.commandType == CommandType.Move;

        bool isAimingAttack =
            isCurrentSlot &&
            batalionManager.commandType == CommandType.Attack;

        Vector3 origin =
            battalion.GetOrderOrigin(slot);

        Vector3 endPoint =
            origin;

        if (hasMove)
        {
            MoveCommand moveCommand =
                (MoveCommand)command;

            endPoint =
                GetVisualMoveEndpoint(
                    slot,
                    origin,
                    moveCommand.pos);
        }
        else if (hasAttack)
        {
            AttackOrder attackOrder =
                (AttackOrder)command;

            endPoint =
                origin +
                attackOrder.direction *
                attackOrder.moveDistance;

            endPoint.z = 0f;
        }

        if (hasMove)
        {
            UpdateMarker(
                orderMarkers,
                orderMarkerPrefab,
                slot,
                true,
                endPoint);
        }
        else if (!isAimingMove)
        {
            HideObject(
                orderMarkers,
                slot);
        }

        if (hasAttack)
        {
            UpdateMarker(
                attackMarkers,
                attackMarkerPrefab,
                slot,
                true,
                endPoint);
        }
        else if (!isAimingAttack)
        {
            HideObject(
                attackMarkers,
                slot);
        }

        Vector3 defendPoint =
            origin;

        if (hasDefend)
        {
            DefendOrder defendOrder =
                (DefendOrder)command;

            defendPoint =
                origin +
                defendOrder.direction.normalized *
                defendOrder.range;

            defendPoint.z = 0f;

            UpdateMarker(
                defendMarkers,
                defendMarkerPrefab,
                slot,
                true,
                defendPoint);
        }
        else
        {
            HideObject(
                defendMarkers,
                slot);
        }

        bool hasOrder =
            hasMove ||
            hasAttack ||
            hasDefend;

        Vector3 arrowEnd =
            hasMove ||
            hasAttack
                ? endPoint
                : defendPoint;

        if (hasOrder || !(isAimingMove || isAimingAttack))
        {
            UpdateOrderArrow(
                slot,
                hasOrder,
                origin,
                arrowEnd);
        }
    }

    // =========================================================
    // MOVE VISUALIZATION
    // =========================================================

    private Vector3 GetVisualMoveEndpoint(
        int slot,
        Vector3 origin,
        Vector3 requestedPoint)
    {
        Vector3 direction = requestedPoint - origin;

        direction.z = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return origin;

        float desiredDistance = direction.magnitude;

        direction.Normalize();

        /*
         * ВАЖЛИВО:
         *
         * Тут не рахуємо terrain вручну.
         *
         * Використовуємо той самий GetReachableDistance(),
         * який використовується системою наказів. Передаємо
         * базовий (terrain-незалежний) battalion.speed — сам
         * GetReachableDistance тепер рахує вартість шляху по
         * ВСЬОМУ маршруту через GetTerrainMoveCost.
         */

        float reachableDistance = battalion.GetReachableDistance(origin, direction, desiredDistance, battalion.battalion.speed, 1f);

        Vector3 result = origin + direction * reachableDistance;

        result.z = 0f;

        return result;
    }

    // =========================================================
    // MARKER
    // =========================================================

    private void UpdateMarker(
        GameObject[] pool,
        GameObject prefab,
        int slot,
        bool show,
        Vector3 point)
    {
        if (slot < 0 ||
            slot >= pool.Length) return;

        if (prefab == null)
            return;

        if (!show)
        {
            if (pool[slot] != null)
                pool[slot].SetActive(false);

            return;
        }

        if (pool[slot] == null)
        {
            pool[slot] = Instantiate(prefab, point, Quaternion.identity);
        }

        pool[slot].SetActive(true);

        pool[slot].transform.position = point;
    }

    // =========================================================
    // ARROW
    // =========================================================

    private void UpdateOrderArrow(
        int slot,
        bool show,
        Vector3 start,
        Vector3 end)
    {
        if (slot < 0 ||
            slot >= orderArrows.Length) return;

        if (arrowPrefab == null)
            return;

        if (!show)
        {
            if (orderArrows[slot] != null)
                orderArrows[slot].SetActive(false);

            return;
        }

        if (orderArrows[slot] == null)
        {
            orderArrows[slot] = Instantiate(arrowPrefab);
        }

        GameObject arrow = orderArrows[slot];

        arrow.SetActive(true);

        LineRenderer line = arrow.GetComponent<LineRenderer>();

        if (line == null)
        {
            Debug.LogError("BattalionVisualsScr: " + "arrowPrefab не має LineRenderer!", arrow);

            return;
        }

        line.useWorldSpace = true;

        line.positionCount = 2;

        start.z = 0f;
        end.z = 0f;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    // =========================================================
    // HIDE
    // =========================================================

    private void HideSlot(int slot)
    {
        HideObject(orderMarkers, slot);
        HideObject(attackMarkers, slot);
        HideObject(defendMarkers, slot);
        HideObject(orderArrows, slot);
    }

    private void HideObject(
        GameObject[] pool,
        int slot)
    {
        if (slot < 0 ||
            slot >= pool.Length) return;

        if (pool[slot] != null)
            pool[slot].SetActive(false);
    }

    private void HideAllVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            HideSlot(i);
        }
    }

    public void ShowPhantomMove(int slot, Vector3 requestedPoint)
    {
        if (battalion == null)
            return;

        if (slot < 0 ||
            slot >= 3)
        {
            return;
        }

        Vector3 origin = battalion.GetOrderOrigin(slot);

        Vector3 endpoint = GetVisualMoveEndpoint(slot, origin, requestedPoint);

        UpdateMarker(orderMarkers, orderMarkerPrefab, slot, true, endpoint);

        UpdateOrderArrow(slot, true, origin, endpoint);
    }

    public void ShowPhantomAttack(
    int slot,
    Vector3 direction,
    float requestedDistance)
    {
        if (battalion == null)
            return;

        if (slot < 0 ||
            slot >= 3)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 origin = battalion.GetOrderOrigin(slot);

        direction.z = 0f;
        direction.Normalize();

        float reachableDistance = battalion.GetReachableDistance(origin, direction, requestedDistance, battalion.battalion.speed, battalion.battalion.attackMoveCostMultiplier);

        Vector3 endpoint = origin + direction * reachableDistance;

        endpoint.z = 0f;

        UpdateMarker(attackMarkers, attackMarkerPrefab, slot, true, endpoint);

        UpdateOrderArrow(slot, true, origin, endpoint);
    }
}