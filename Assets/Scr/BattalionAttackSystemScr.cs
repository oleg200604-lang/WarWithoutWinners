using System.Collections.Generic;
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
        if (attacker == null)
            return null;

        if (direction.sqrMagnitude < 0.001f)
            return null;

        direction.Normalize();

        Vector3 origin = attacker.transform.position;

        TerrainManagerScr terrain = TerrainManagerScr.Instance;

        int attackerHeight =
            terrain != null
                ? terrain.GetHeightAt(origin)
                : 0;

        bool attackerIsArtillery = attacker.battalion.type == BattalionType.artillery;

        LayerMask combinedMask = battalionLayer;

        if (terrain != null)
            combinedMask |= terrain.TerrainLayer;

        float startAngle = -coneAngle * 0.5f;

        float angleStep =
            rayCount > 1
                ? coneAngle / (rayCount - 1)
                : 0f;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = startAngle + angleStep * i;

            Vector3 rayDirection = Quaternion.Euler(0f, 0f, angle) * direction;

            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, rayDirection, maxAttackRange, combinedMask);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                TerrainTileScr tile = hit.collider.GetComponent<TerrainTileScr>();

                if (tile != null)
                {
                    if (terrain != null &&
                        terrain.BlocksLineOfSight(
                            tile.type))
                    {
                        break;
                    }

                    continue;
                }

                BattalionScr target = hit.collider.GetComponent<BattalionScr>();

                if (target == null)
                    continue;

                if (target == attacker)
                    continue;

                if (target.teamID ==
                    attacker.teamID)
                {
                    continue;
                }

                float allowedRange = maxAttackRange;

                if (!attackerIsArtillery &&
                    terrain != null)
                {
                    int targetHeight = terrain.GetHeightAt(target.transform.position);

                    int heightDifference = attackerHeight - targetHeight;

                    if (heightDifference > 0)
                    {
                        allowedRange *= 1f + 0.1f * heightDifference;
                    }
                }

                if (hit.distance <= allowedRange)
                    return target;
            }
        }

        return null;
    }
    public List<BattalionScr> FindTargetsInRadius(Vector3 center, float radius, BattalionScr attacker)
    {
        List<BattalionScr> results = new List<BattalionScr>();

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, battalionLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            BattalionScr target = hit.GetComponent<BattalionScr>();

            if (target == null)
                continue;

            if (target == attacker)
                continue;

            results.Add(target);
        }

        return results;
    }
}