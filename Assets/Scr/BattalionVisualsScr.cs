using UnityEngine;

[RequireComponent(typeof(BattalionScr))]
public class BattalionVisualsScr : MonoBehaviour
{
    private BattalionScr battalion;
    private BatalionManagerScr batalionManager;

    [Header("Мітка точки наказу")]
    public GameObject orderMarkerPrefab;

    [Header("Стрілка наказу")]
    public GameObject arrowPrefab;

    private GameObject[] orderMarkers = new GameObject[3];
    private GameObject[] orderArrows = new GameObject[3];

    private void Awake()
    {
        battalion = GetComponent<BattalionScr>();

        if (battalion != null)
            batalionManager = battalion.batalionManager;
    }

    private void Start()
    {
        // Якщо посилання не було встановлене до Awake,
        // пробуємо знайти менеджер ще раз.
        if (batalionManager == null && battalion != null)
            batalionManager = battalion.batalionManager;

        if (batalionManager == null)
        {
            Debug.LogError(
                $"BattalionVisualsScr: {name} не має BatalionManagerScr!",
                this
            );
        }
    }

    private void Update()
    {
        if (battalion == null || batalionManager == null)
            return;

        bool isSelected = batalionManager.selectBattalion == battalion;

        for (int i = 0; i < 3; i++)
        {
            bool hasMoveOrder =
                isSelected &&
                battalion.command[i] is MoveCommand move &&
                move.isSet;

            UpdateOrderMarker(i, hasMoveOrder);
            UpdateOrderArrow(i, hasMoveOrder);
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

        MoveCommand move = battalion.command[slot] as MoveCommand;

        if (move == null || !move.isSet)
            return;

        if (orderMarkers[slot] == null)
        {
            orderMarkers[slot] =
                Instantiate(orderMarkerPrefab, move.pos, Quaternion.identity);
        }

        orderMarkers[slot].SetActive(true);
        orderMarkers[slot].transform.position = move.pos;
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

        MoveCommand move = battalion.command[slot] as MoveCommand;

        if (move == null || !move.isSet)
            return;

        Vector3 start = battalion.GetOrderOrigin(slot);
        Vector3 end = move.pos;

        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance < 0.001f)
            return;

        if (orderArrows[slot] == null)
        {
            orderArrows[slot] = Instantiate(arrowPrefab);
        }

        GameObject arrow = orderArrows[slot];

        arrow.SetActive(true);

        arrow.transform.position = (start + end) * 0.5f;

        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        arrow.transform.rotation =
            Quaternion.Euler(0f, 0f, angle);

        arrow.transform.localScale =
            new Vector3(distance, 1f, 1f);
    }
}