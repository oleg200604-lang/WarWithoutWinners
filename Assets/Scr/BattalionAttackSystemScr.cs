using UnityEngine;

public class BattalionAttackSystemScr : MonoBehaviour
{
    [SerializeField] private int rayCount = 31;
    [SerializeField] private float step = 0.25f;
    [SerializeField] private LayerMask battalionLayer;

    public BattalionScr FindTarget(
    BattalionScr attacker,
    Vector3 direction,
    float maxAttackRange)
    {
        Vector3 origin = attacker.transform.position;

        float startAngle = -45f;
        float angleStep = 90f / (rayCount - 1);

        for (int i = 0; i < rayCount; i++)
        {
            float angle = startAngle + angleStep * i;

            Vector3 rayDirection =
                Quaternion.Euler(0f, 0f, angle) * direction;

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                rayDirection,
                maxAttackRange,
                battalionLayer
            );

            if (hit.collider == null)
                continue;

            BattalionScr target =
                hit.collider.GetComponent<BattalionScr>();

            if (target == null)
                continue;

            if (target.teamID == attacker.teamID)
                continue;

            return target;
        }

        return null;
    }

    private BattalionScr ScanRay(BattalionScr attacker, Vector3 origin, Vector3 direction, float range)
    {
        for (float distance = step;
             distance <= range;
             distance += step)
        {
            Vector3 point =
                origin + direction * distance;

            Collider2D hit = Physics2D.OverlapCircle(
                point,
                0.1f,
                battalionLayer
            );

            if (hit == null)
                continue;

            BattalionScr target =
                hit.GetComponent<BattalionScr>();

            if (target == null)
                continue;

            if (target.teamID == attacker.teamID)
                continue;

            return target;
        }

        return null;
    }
}
