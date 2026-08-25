using System.Collections.Generic;
using UnityEngine;

public class BattalionAttackSystemScr : MonoBehaviour
{
    [SerializeField] private int rayCount = 31;
    [SerializeField] private float step = 0.25f;
    [SerializeField] private LayerMask battalionLayer;

    public int RayCount => rayCount;

    public float GetConeAngle(BattalionScr attacker)
    {
        if (attacker == null ||
            attacker.battalion == null)
        {
            return 90f;
        }

        return attacker.battalion.attackConeAngle;
    }

    /// <summary>
    /// Дальність до першого terrain, що блокує лінію вогню вздовж
    /// напрямку — та сама перевірка, що й у FindTarget, але без
    /// пошуку цілі. Використовується прев'ю-візуалізацією
    /// (RangeIndicatorScr), щоб промені видимо зупинялись на
    /// лісі/горах так само, як реально зупиняється атака.
    /// </summary>
    public float GetLineOfSightRange(Vector3 origin, Vector3 direction, float maxRange)
    {
        if (direction.sqrMagnitude < 0.001f || maxRange <= 0f)
            return 0f;

        direction.Normalize();

        TerrainManagerScr terrain = TerrainManagerScr.Instance;

        if (terrain == null)
            return maxRange;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, maxRange, terrain.TerrainLayer);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            TerrainTileScr tile = hit.collider.GetComponent<TerrainTileScr>();

            if (tile != null && terrain.BlocksLineOfSight(tile.type))
                return hit.distance;
        }

        return maxRange;
    }

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

        float coneAngle = GetConeAngle(attacker);

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
            float angle =
                startAngle +
                angleStep * i;

            Vector3 rayDirection =
                Quaternion.Euler(0f, 0f, angle) *
                direction;

            RaycastHit2D[] hits =
                Physics2D.RaycastAll(
                    origin,
                    rayDirection,
                    maxAttackRange,
                    combinedMask);

            System.Array.Sort(
                hits,
                (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                TerrainTileScr tile =
                    hit.collider.GetComponent<TerrainTileScr>();

                if (tile != null)
                {
                    if (terrain != null &&
                        terrain.BlocksLineOfSight(tile.type))
                    {
                        break;
                    }

                    continue;
                }

                BattalionScr target =
                    hit.collider.GetComponent<BattalionScr>();

                if (target == null)
                    continue;

                if (target == attacker)
                    continue;

                if (target.teamID == attacker.teamID)
                    continue;

                float allowedRange = maxAttackRange;

                if (terrain != null)
                {
                    int targetHeight =
                        terrain.GetHeightAt(
                            target.transform.position);

                    int heightDifference =
                        attackerHeight -
                        targetHeight;

                    if (heightDifference > 0)
                    {
                        allowedRange *=
                            1f +
                            0.1f *
                            heightDifference;
                    }
                }

                if (hit.distance <= allowedRange)
                    return target;
            }
        }

        return null;
    }

    public List<BattalionScr> FindTargetsInRadius(
        Vector3 center,
        float radius,
        BattalionScr attacker)
    {
        List<BattalionScr> results =
            new List<BattalionScr>();

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                center,
                radius,
                battalionLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            BattalionScr target =
                hit.GetComponent<BattalionScr>();

            if (target == null)
                continue;

            if (target == attacker)
                continue;

            if (target.teamID == attacker.teamID)
                continue;

            results.Add(target);
        }

        return results;
    }
}