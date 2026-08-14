using UnityEngine;
public class RangeIndicatorScr : MonoBehaviour
{
    public BatalionManagerScr batalionManager;

    [Header("Заливка (без контура)")]
    public MeshFilter meshFilter;
    public Color fillColor = new Color(0.2f, 1f, 0.3f, 0.25f);
    public int segments = 48;

    private Mesh mesh;
    private MeshRenderer meshRenderer;

    private void Update()
    {
        if (meshFilter == null)
        {
            Debug.LogWarning("RangeIndicatorScr: не призначено meshFilter.", this);
            return;
        }
        if (batalionManager == null)
        {
            Debug.LogWarning("RangeIndicatorScr: не призначено batalionManager.", this);
            return;
        }

        EnsureMesh();

        BattalionScr selected = batalionManager.selectBattalion;
        if (selected == null)
        {
            Hide();
            return;
        }

        int slot = batalionManager.CommandDuty;
        Vector3 origin = selected.GetOrderOrigin(slot);
        float radius = selected.GetRemainingRange(slot);

        if (radius <= 0f)
        {
            Hide();
            return;
        }

        Show();
        BuildCircle(origin, radius);
    }

    private void EnsureMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "RangeFillMesh" };
            meshFilter.mesh = mesh;
        }
        if (meshRenderer == null)
            meshRenderer = meshFilter.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            Debug.LogWarning("RangeIndicatorScr: на об'єкті meshFilter немає MeshRenderer.", this);
    }

    private void Hide()
    {
        mesh.Clear();
        if (meshRenderer != null)
            meshRenderer.enabled = false;
    }

    private void Show()
    {
        if (meshRenderer != null)
            meshRenderer.enabled = true;
    }

    private void BuildCircle(Vector3 origin, float radius)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[vertices.Length];

        // Подвійна намотка трикутників (лицева + зворотна), щоб меш було
        // видно незалежно від напрямку камери — саме це найімовірніше й
        // ламало "не працює": одностороння намотка не збігалась з тим,
        // як дивиться камера в проекті, і backface culling ховав меш.
        int[] triangles = new int[segments * 3 * 2];

        Transform t = meshFilter.transform;
        vertices[0] = t.InverseTransformPoint(origin);
        colors[0] = fillColor;

        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            Vector3 worldPoint = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            vertices[i + 1] = t.InverseTransformPoint(worldPoint);
            colors[i + 1] = fillColor;
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int b = i * 6;

            triangles[b] = 0;
            triangles[b + 1] = i + 1;
            triangles[b + 2] = next + 1;

            triangles[b + 3] = 0;
            triangles[b + 4] = next + 1;
            triangles[b + 5] = i + 1;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
    }
}