using UnityEngine;

/// <summary>
/// Тільки візуалізація для одного батальйону: мітки точок наказів і стрілки
/// між ними. Показує їх лише поки battalion == batalionManager.selectBattalion.
/// Ніякої ігрової логіки тут немає — вся вона в BattalionScr.
/// Вішається на той самий GameObject, що й BattalionScr.
/// </summary>
[RequireComponent(typeof(BattalionScr))]
public class BattalionVisualsScr : MonoBehaviour
{
    public BattalionScr battalion;
    public BatalionManagerScr batalionManager;

    [Header("Мітки наказу руху (показуються тільки коли батальйон обраний)")]
    public GameObject orderMarkerPrefab;
    private GameObject[] orderMarkers = new GameObject[3];

    [Header("Мітки наказу атаки")]
    public GameObject attackMarkerPrefab;
    private GameObject[] attackMarkers = new GameObject[3];

    [Header("Мітки наказу захисту")]
    public GameObject defendMarkerPrefab;
    private GameObject[] defendMarkers = new GameObject[3];

    [Header("Стрілки між точками наказів")]
    [Tooltip("Префаб має бути 1 юніт завдовжки вздовж локальної осі +X, з піботом по центру.")]
    public GameObject arrowPrefab;
    private GameObject[] orderArrows = new GameObject[3];

    private void Reset()
    {
        battalion = GetComponent<BattalionScr>();
    }

    private void Update()
    {
        if (battalion == null)
        {
            Debug.LogWarning("BattalionVisualsScr: не призначено battalion.", this);
            return;
        }
        if (batalionManager == null)
        {
            Debug.LogWarning("BattalionVisualsScr: не призначено batalionManager.", this);
            return;
        }

        bool isSelected = batalionManager.selectBattalion == battalion;

        for (int i = 0; i < battalion.command.Length; i++)
        {
            bool hasMove = isSelected && battalion.command[i] is MoveCommand move && move.isSet;
            bool hasAttack = isSelected && battalion.command[i] is AttackOrder attack && attack.isSet;
            bool hasDefend = isSelected && battalion.command[i] is DefendOrder defend && defend.isSet;

            Vector3 movePoint = hasMove ? ((MoveCommand)battalion.command[i]).pos : default;

            Vector3 attackPoint = default;
            if (hasAttack)
            {
                AttackOrder attackOrder = (AttackOrder)battalion.command[i];
                attackPoint = battalion.GetOrderOrigin(i) + attackOrder.direction * attackOrder.range;
            }

            Vector3 defendPoint = default;
            if (hasDefend)
            {
                DefendOrder defendOrder = (DefendOrder)battalion.command[i];
                defendPoint = battalion.GetOrderOrigin(i) + defendOrder.direction * defendOrder.range;
            }

            UpdateMarker(orderMarkers, orderMarkerPrefab, i, hasMove, movePoint);
            UpdateMarker(attackMarkers, attackMarkerPrefab, i, hasAttack, attackPoint);
            UpdateMarker(defendMarkers, defendMarkerPrefab, i, hasDefend, defendPoint);

            Vector3 arrowEnd = hasMove ? movePoint : (hasAttack ? attackPoint : defendPoint);
            UpdateOrderArrow(i, hasMove || hasAttack || hasDefend, battalion.GetOrderOrigin(i), arrowEnd);
        }
    }

    private void UpdateMarker(GameObject[] pool, GameObject prefab, int slot, bool show, Vector3 point)
    {
        if (prefab == null)
            return;

        if (!show)
        {
            if (pool[slot] != null)
                pool[slot].SetActive(false);
            return;
        }

        if (pool[slot] == null)
            pool[slot] = Instantiate(prefab, point, Quaternion.identity);

        pool[slot].SetActive(true);
        pool[slot].transform.position = point;
    }

    private void UpdateOrderArrow(int slot, bool show, Vector3 start, Vector3 end)
    {
        if (arrowPrefab == null)
            return;

        if (!show)
        {
            if (orderArrows[slot] != null)
                orderArrows[slot].SetActive(false);
            return;
        }

        Vector3 dir = end - start;
        float distance = dir.magnitude;

        if (orderArrows[slot] == null)
            orderArrows[slot] = Instantiate(arrowPrefab);

        orderArrows[slot].SetActive(true);
        orderArrows[slot].transform.position = (start + end) * 0.5f;

        if (distance > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            orderArrows[slot].transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        orderArrows[slot].transform.localScale = new Vector3(distance, 1f, 1f);
    }
}