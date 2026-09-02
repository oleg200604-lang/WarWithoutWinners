using System.Collections.Generic;
using UnityEngine;

// "Void" — окрема зона поповнення боєприпасами. Розміщується на сцені
// (наприклад, у тилу/на базі); всі батальйони своєї команди, що
// опиняються в радіусі дії на момент завершення ходу (NextMove ->
// ResupplyInRange), отримують боєприпаси з глобального запасу
// ressurs.supplies відповідного BatalionManagerScr.
public class AmmoDepotScr : MonoBehaviour
{
    [Tooltip("Команда, батальйонам якої цей пункт поповнює боєприпаси.")]
    public int teamID;

    [Tooltip("Радіус дії пункту поповнення.")]
    public float radius = 2f;

    [Tooltip("Скільки боєприпасів максимум видається одному батальйону за один хід.")]
    public int resupplyPerTurnPerBattalion = 30;

    // Викликається BatalionManagerScr.NextMove() під час завершення ходу.
    // manager — той самий менеджер, з чийого списку ammoDepots взято цей
    // пункт (звідти береться глобальний запас ressurs.supplies).
    public void ResupplyInRange(BatalionManagerScr manager)
    {
        if (manager == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);

        if (hits == null || hits.Length == 0)
            return;

        List<BattalionScr> alreadyHandled = new List<BattalionScr>();

        for (int i = 0; i < hits.Length; i++)
        {
            BattalionScr battalion = hits[i].GetComponent<BattalionScr>();

            if (battalion == null)
                continue;

            if (battalion.teamID != teamID)
                continue;

            if (alreadyHandled.Contains(battalion))
                continue;

            alreadyHandled.Add(battalion);

            int missing = battalion.ammo.max - battalion.ammo.current;

            if (missing <= 0)
                continue;

            int amount = Mathf.Min(missing, resupplyPerTurnPerBattalion, manager.ressurs.supplies);

            if (amount <= 0)
                continue;

            manager.ressurs.supplies -= amount;
            battalion.ammo.Add(amount);

            Debug.Log(battalion.nameBattalion + ": отримано " + amount + " боєприпасів у зоні поповнення.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
        Gizmos.DrawSphere(transform.position, radius);
    }
}