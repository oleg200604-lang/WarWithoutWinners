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

    [Header("Мітки наказів (показуються тільки коли батальйон обраний)")]
    public GameObject orderMarkerPrefab;
    private GameObject[] orderMarkers = new GameObject[3];

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
            bool hasOrder = isSelected && battalion.command[i] is MoveCommand move && move.isSet;
            UpdateOrderMarker(i, hasOrder);
            UpdateOrderArrow(i, hasOrder);
        }
    }

    private void UpdateOrderMarker(int slot, bool show)
    {
        if (orderMarkerPrefab == null)
            return;

        if (!show)
        {
            if (orderMarkers[slot] != null)
                orderMarkers[slot].SetActive(false);
            return;
        }

        Vector3 pos = ((MoveCommand)battalion.command[slot]).pos;
        if (orderMarkers[slot] == null)
            orderMarkers[slot] = Instantiate(orderMarkerPrefab, pos, Quaternion.identity);

        orderMarkers[slot].SetActive(true);
        orderMarkers[slot].transform.position = pos;
    }

    private void UpdateOrderArrow(int slot, bool show)
    {
        if (arrowPrefab == null)
            return;

        if (!show)
        {
            if (orderArrows[slot] != null)
                orderArrows[slot].SetActive(false);
            return;
        }

        Vector3 start = battalion.GetOrderOrigin(slot);
        Vector3 end = ((MoveCommand)battalion.command[slot]).pos;
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