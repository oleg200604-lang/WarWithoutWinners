using UnityEngine;

public class BattalionAttackSystemScr : MonoBehaviour
{
    [SerializeField] private int rayCount = 31;
    [SerializeField] private float step = 0.25f;
    [SerializeField] private float coneAngle = 90f;
    [SerializeField] private LayerMask battalionLayer;

    // Публічний доступ для прев'ю-візуалізації (RangeIndicatorScr), щоб
    // "всі промені" на екрані завжди збігались із тим, що реально рахує
    // FindTarget — жодних задубльованих магічних чисел в двох місцях.
    public int RayCount => rayCount;
    public float ConeAngle => coneAngle;

    public BattalionScr FindTarget(
        BattalionScr attacker,
        Vector3 direction,
        float maxAttackRange)
    {
        Vector3 origin = attacker.transform.position;

        float startAngle = -coneAngle * 0.5f;
        float angleStep = rayCount > 1 ? coneAngle / (rayCount - 1) : 0f;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = startAngle + angleStep * i;

            Vector3 rayDirection =
                Quaternion.Euler(0f, 0f, angle) * direction;

            // RaycastAll, а не Raycast: origin лежить УСЕРЕДИНІ власного
            // колайдера атакуючого батальйона, тож звичайний одиничний
            // Raycast майже завжди впирається сам у себе на дистанції ~0
            // і ніколи не долітає до цілі позаду. RaycastAll повертає всі
            // перетини вздовж променя — власний колайдер просто
            // пропускаємо (target == attacker) і перевіряємо далі.
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                rayDirection,
                maxAttackRange,
                battalionLayer
            );

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit2D hit in hits)
            {
                BattalionScr target = hit.collider.GetComponent<BattalionScr>();

                if (target == null || target == attacker)
                    continue;

                if (target.teamID == attacker.teamID)
                    continue;

                return target;
            }
        }

        return null;
    }
}