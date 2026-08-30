using System.Collections.Generic;
using UnityEngine;

// Малює лінію між кожною сусідньою (по ланцюгу) парою батальйонів у
// кожному полку — щоб було видно з першого погляду, хто з ким об'єднаний,
// незалежно від того, чи цей полк зараз вибраний.
//
// Лінія жовтіє -> червоніє, коли відстань між сусідами наближається до
// regiment.chainMaxDistance — тобто заодно видно, наскільки ланцюг
// "натягнутий".
public class RegimentChainVisualsScr : MonoBehaviour
{
    public BatalionManagerScr batalionManager;

    [Header("Колір ланцюга (жовтий -> червоний ближче до ліміту)")]
    public Color chainColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    public Color strainedColor = new Color(1f, 0.15f, 0.15f, 0.9f);
    public float lineWidth = 0.06f;

    private readonly List<LineRenderer> pool = new List<LineRenderer>();

    private LineRenderer GetLine(int index)
    {
        while (pool.Count <= index)
        {
            GameObject go = new GameObject("RegimentChainLine_" + pool.Count);
            go.transform.SetParent(transform, false);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = lineWidth;
            lr.positionCount = 2;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                lr.material = new Material(shader);

            pool.Add(lr);
        }

        return pool[index];
    }

    private void HideFrom(int startIndex)
    {
        for (int i = startIndex; i < pool.Count; i++)
        {
            if (pool[i] != null)
                pool[i].enabled = false;
        }
    }

    private void Update()
    {
        if (batalionManager == null || batalionManager.regiments == null)
        {
            HideFrom(0);
            return;
        }

        int used = 0;

        for (int r = 0; r < batalionManager.regiments.Count; r++)
        {
            Regiment regiment = batalionManager.regiments[r];

            if (regiment == null || regiment.battalions == null)
                continue;

            for (int i = 0; i < regiment.battalions.Count - 1; i++)
            {
                BattalionScr a = regiment.battalions[i];
                BattalionScr b = regiment.battalions[i + 1];

                if (a == null || b == null)
                    continue;

                Vector3 posA = a.transform.position;
                Vector3 posB = b.transform.position;
                posA.z = 0f;
                posB.z = 0f;

                LineRenderer line = GetLine(used);
                used++;

                line.SetPosition(0, posA);
                line.SetPosition(1, posB);

                float distance = Vector3.Distance(posA, posB);
                float limit = Mathf.Max(0.01f, regiment.chainMaxDistance);
                float t = Mathf.Clamp01(distance / limit);

                Color color = Color.Lerp(chainColor, strainedColor, t);
                line.startColor = color;
                line.endColor = color;

                line.enabled = true;
            }
        }

        HideFrom(used);
    }
}