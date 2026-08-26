using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Прев'ю наказу, який гравець зараз готує (до правого кліку):
/// — Move: заповнене коло дальності пересування;
/// — Attack / Defend: коло фіксованої дальності (selected.GetEffectiveAttackRange(),
///   з поправкою на ліс, не залежить від залишку Speed) + віяло променів (rayCount/ConeAngle
///   обраного батальйона), наведене на курсор миші. Attack і Defend
///   розрізняються лише кольором.
/// Показує РІВНО ОДНЕ з трьох — залежно від batalionManager.commandType.
/// </summary>
public class RangeIndicatorScr : MonoBehaviour
{
    public BatalionManagerScr batalionManager;

    [Header("Заливка (без контура)")]
    public MeshFilter meshFilter;
    public Color fillColor = new Color(0.2f, 1f, 0.3f, 0.25f);         // Move
    public Color attackFillColor = new Color(1f, 0.25f, 0.25f, 0.25f); // Attack
    public Color defendFillColor = new Color(0.25f, 0.45f, 1f, 0.25f); // Defend
    public int segments = 48;

    [Header("Промені атаки/захисту")]
    public LineRenderer rayFanLineRenderer;
    public Color rayColor = new Color(1f, 0.3f, 0.3f, 0.6f);
    public Color defendRayColor = new Color(0.3f, 0.5f, 1f, 0.6f);

    [Header("Bombard preview")]
    public MeshFilter bombardRadiusMeshFilter;
    public GameObject bombardCenterPrefab;

    private Mesh bombardRadiusMesh;
    private MeshRenderer bombardRadiusRenderer;
    private GameObject bombardCenterMarker;
    private Mesh mesh;
    private MeshRenderer meshRenderer;

    private BattalionScr cachedVisualsOwner;
    private BattalionVisualsScr cachedVisuals;

    private BattalionVisualsScr GetVisuals(BattalionScr selected)
    {
        if (selected != cachedVisualsOwner)
        {
            cachedVisualsOwner = selected;
            cachedVisuals = selected != null ? selected.GetComponent<BattalionVisualsScr>() : null;
        }

        return cachedVisuals;
    }

    // Клас: RangeIndicatorScr
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

        CommandType commandType = batalionManager.commandType;

        if (selected == null)
        {
            Hide();
            HideRayFan();
            HideBombardRadius();
            return;
        }

        int slot = batalionManager.CommandDuty;

        Vector3 origin = selected.GetOrderOrigin(slot);

        if (commandType == CommandType.Move)
        {
            HideRayFan();
            HideBombardRadius();

            float availableSpeed = selected.battalion.speed;

            if (availableSpeed <= 0f)
            {
                Hide();
                return;
            }

            Show();

            BuildMoveArea(selected, origin, availableSpeed, fillColor);

            BattalionVisualsScr visuals = GetVisuals(selected);

            if (visuals != null)
                visuals.ShowPhantomMove(slot, GetMouseWorldPosition());

            return;
        }

        if (commandType == CommandType.Attack)
        {
            HideBombardRadius();

            if (selected.GetProjectedDeployedState(slot))
            {
                Hide();
                HideRayFan();
                return;
            }

            Vector3 aimDirection = GetAimDirection(origin);

            Vector3 mouseWorld = GetMouseWorldPosition();

            float desiredDistance = (mouseWorld - origin).magnitude;

            float moveDistance =
                selected.GetReachableDistance(
                    origin,
                    aimDirection,
                    desiredDistance,
                    selected.battalion.speed,
                    selected.battalion.attackMoveCostMultiplier);

            Vector3 attackOrigin = origin + aimDirection * moveDistance;

            attackOrigin.z = 0f;

            float maxRange = selected.GetEffectiveAttackRange(attackOrigin);

            if (maxRange <= 0f)
            {
                Hide();
                HideRayFan();
                return;
            }

            Show();

            BuildCircle(attackOrigin, maxRange, attackFillColor);

            ShowRayFan(selected, attackOrigin, aimDirection, maxRange, rayColor);

            BattalionVisualsScr visuals = GetVisuals(selected);

            if (visuals != null)
                visuals.ShowPhantomAttack(slot, aimDirection, desiredDistance);

            return;
        }

        if (commandType == CommandType.Defend)
        {
            HideBombardRadius();

            bool isDeployed = selected.GetProjectedDeployedState(slot);

            float maxRange =
                isDeployed
                    ? selected.deployRange
                    : selected.GetEffectiveAttackRange(origin);

            if (maxRange <= 0f)
            {
                Hide();
                HideRayFan();
                return;
            }

            Show();

            HideRayFan();

            BuildCircle(origin, maxRange, defendFillColor);

            return;
        }

        if (commandType == CommandType.Bombard)
        {
            HideRayFan();

            if (!selected.GetProjectedDeployedState(slot))
            {
                Hide();
                HideBombardRadius();
                return;
            }

            float range = selected.deployRange;

            Vector3 deployDirection = selected.GetProjectedDeployDirection(slot);

            // Основна зона дії артилерії.
            Show();

            BuildSector(origin, range, deployDirection, selected.deployConeAngle, attackFillColor);

            // Центр і радіус вибуху навколо курсора.
            Vector3 mouseWorld = GetMouseWorldPosition();

            ShowBombardRadius(mouseWorld, selected.bombardRadius);

            return;
        }

        if (commandType == CommandType.Rotate)
        {
            HideBombardRadius();

            if (!selected.GetProjectedDeployedState(slot))
            {
                Hide();
                HideRayFan();
                return;
            }

            Show();

            BuildSector(origin, selected.deployRange, selected.GetProjectedDeployDirection(slot), selected.deployConeAngle, defendFillColor);

            ShowRayFan(selected, origin, GetAimDirection(origin), selected.deployRange, defendRayColor);

            return;
        }

        if (commandType == CommandType.Deploy)
        {
            HideBombardRadius();
            HideRayFan();

            bool willDeploy = !selected.GetProjectedDeployedState(slot);

            if (!willDeploy)
            {
                Hide();
                return;
            }

            Show();

            Vector3 direction = GetAimDirection(origin);

            BuildSector(origin, selected.deployRange, direction, selected.deployConeAngle, attackFillColor);

            return;
        }

        Hide();
        HideRayFan();
        HideBombardRadius();
    }

    private void HideBombardRadius()
    {
        if (bombardRadiusRenderer != null)
            bombardRadiusRenderer.enabled = false;

        if (bombardCenterMarker != null)
            bombardCenterMarker.SetActive(false);
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (Mouse.current == null ||
            Camera.main == null)
        {
            return Vector3.zero;
        }

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        mouseWorld.z = 0f;

        return mouseWorld;
    }

    private void BuildSector(Vector3 origin, float radius, Vector3 direction, float coneAngle, Color color)
    {
        int safeSegments = Mathf.Max(8, segments);

        Vector3[] vertices = new Vector3[safeSegments + 2];

        Color[] colors = new Color[vertices.Length];

        int[] triangles = new int[safeSegments * 6];

        Transform t = meshFilter.transform;

        vertices[0] = t.InverseTransformPoint(origin);

        colors[0] = color;

        direction.z = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.right;

        direction.Normalize();

        float startAngle = -coneAngle * 0.5f;

        float step = coneAngle / safeSegments;

        for (int i = 0; i <= safeSegments; i++)
        {
            float angle = startAngle + step * i;

            Vector3 pointDirection = Quaternion.Euler(0f, 0f, angle) * direction;

            Vector3 worldPoint = origin + pointDirection * radius;

            vertices[i + 1] = t.InverseTransformPoint(worldPoint);

            colors[i + 1] = color;
        }

        for (int i = 0; i < safeSegments; i++)
        {
            int next = i + 1;
            int triangle = i * 6;

            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = next + 1;

            triangles[triangle + 3] = 0;
            triangles[triangle + 4] = next + 1;
            triangles[triangle + 5] = i + 1;
        }

        mesh.Clear();

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
    }

    private void ShowBombardRadius(Vector3 center, float radius)
    {
        EnsureBombardRadius();

        if (bombardRadiusMeshFilter == null ||
            bombardRadiusRenderer == null)
        {
            return;
        }

        BuildBombardRadius(center, radius);

        bombardRadiusRenderer.enabled = true;

        if (bombardCenterPrefab != null)
        {
            if (bombardCenterMarker == null)
            {
                bombardCenterMarker = Instantiate(bombardCenterPrefab);
            }

            bombardCenterMarker.SetActive(true);

            bombardCenterMarker.transform.position = center;
        }
    }

    private void BuildBombardRadius(Vector3 center, float radius)
    {
        int safeSegments = Mathf.Max(24, segments);

        Vector3[] vertices = new Vector3[safeSegments + 1];

        Color[] colors = new Color[vertices.Length];

        int[] triangles = new int[safeSegments * 6];

        Transform t = bombardRadiusMeshFilter.transform;

        vertices[0] = t.InverseTransformPoint(center);

        colors[0] = attackFillColor;

        for (int i = 0; i < safeSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / safeSegments;

            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

            vertices[i + 1] = t.InverseTransformPoint(point);

            colors[i + 1] = attackFillColor;
        }

        for (int i = 0; i < safeSegments; i++)
        {
            int next = (i + 1) % safeSegments;

            int triangle = i * 6;

            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = next + 1;

            triangles[triangle + 3] = 0;
            triangles[triangle + 4] = next + 1;
            triangles[triangle + 5] = i + 1;
        }

        bombardRadiusMesh.Clear();

        bombardRadiusMesh.vertices = vertices;

        bombardRadiusMesh.colors = colors;

        bombardRadiusMesh.triangles = triangles;

        bombardRadiusMesh.RecalculateBounds();
    }

    private void EnsureBombardRadius()
    {
        if (bombardRadiusMeshFilter == null)
            return;

        if (bombardRadiusMesh == null)
        {
            bombardRadiusMesh =
                new Mesh
                {
                    name = "BombardRadiusMesh"
                };

            bombardRadiusMeshFilter.mesh = bombardRadiusMesh;
        }

        if (bombardRadiusRenderer == null)
        {
            bombardRadiusRenderer =
                bombardRadiusMeshFilter
                    .GetComponent<MeshRenderer>();
        }
    }

    private void ShowRayFan(BattalionScr selected, Vector3 origin, Vector3 direction, float range, Color color)
    {
        LineRenderer rayFan = EnsureRayFan();

        if (rayFan == null)
            return;

        int rayCount = 31;
        float coneAngle = 90f;

        if (selected.attackSystem != null)
        {
            rayCount =
                Mathf.Max(
                    1,
                    selected.attackSystem.RayCount);

            coneAngle =
                selected.attackSystem.GetConeAngle(
                    selected);
        }

        float startAngle =
            -coneAngle * 0.5f;

        float angleStep =
            rayCount > 1
                ? coneAngle / (rayCount - 1)
                : 0f;

        Vector3[] points =
            new Vector3[rayCount * 2];

        for (int i = 0; i < rayCount; i++)
        {
            float angle =
                startAngle +
                angleStep * i;

            Vector3 rayDirection =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle) *
                direction;

            float visibleRange = range;

            if (selected.attackSystem != null)
            {
                visibleRange =
                    selected.attackSystem.GetLineOfSightRange(
                        origin,
                        rayDirection,
                        range);
            }

            points[i * 2] =
                origin;

            points[i * 2 + 1] =
                origin +
                rayDirection * visibleRange;
        }

        rayFan.positionCount =
            points.Length;

        rayFan.SetPositions(points);

        rayFan.startColor =
            color;

        rayFan.endColor =
            color;

        rayFan.enabled = true;
    }

    private LineRenderer EnsureRayFan()
    {
        if (rayFanLineRenderer != null)
            return rayFanLineRenderer;

        GameObject go = new GameObject("RayFanLineRenderer (auto)");
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.05f;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            lr.material = new Material(shader);

        rayFanLineRenderer = lr;
        return rayFanLineRenderer;
    }

    private void HideRayFan()
    {
        if (rayFanLineRenderer != null)
            rayFanLineRenderer.enabled = false;
    }

    private Vector3 GetAimDirection(Vector3 origin)
    {
        if (Mouse.current == null || Camera.main == null)
            return Vector3.right;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0;

        Vector3 direction = mouseWorld - origin;
        direction.z = 0;

        if (direction.sqrMagnitude < 0.001f)
            return Vector3.right;

        return direction.normalized;
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

    private void BuildCircle(Vector3 origin, float radius, Color color)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[vertices.Length];

        int[] triangles = new int[segments * 3 * 2];

        Transform t = meshFilter.transform;
        vertices[0] = t.InverseTransformPoint(origin);
        colors[0] = color;

        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            Vector3 worldPoint = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            vertices[i + 1] = t.InverseTransformPoint(worldPoint);
            colors[i + 1] = color;
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

    private void BuildMoveArea(BattalionScr selected, Vector3 origin, float availableSpeed, Color color)
    {
        int safeSegments = Mathf.Max(12, segments);

        Vector3[] vertices = new Vector3[safeSegments + 1];

        Color[] colors = new Color[vertices.Length];

        int[] triangles = new int[safeSegments * 6];

        Transform t = meshFilter.transform;

        vertices[0] = t.InverseTransformPoint(origin);

        colors[0] = color;

        float testDistance = Mathf.Max(0f, availableSpeed * 2.05f);

        for (int i = 0; i < safeSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / safeSegments;

            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            float reachableDistance = selected.GetReachableDistance(origin, direction, testDistance, availableSpeed);

            Vector3 worldPoint = origin + direction * reachableDistance;

            vertices[i + 1] = t.InverseTransformPoint(worldPoint);

            colors[i + 1] = color;
        }

        for (int i = 0; i < safeSegments; i++)
        {
            int next = (i + 1) % safeSegments;

            int triangle = i * 6;

            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = next + 1;

            triangles[triangle + 3] = 0;
            triangles[triangle + 4] = next + 1;
            triangles[triangle + 5] = i + 1;
        }

        mesh.Clear();

        mesh.vertices = vertices;

        mesh.colors = colors;

        mesh.triangles = triangles;

        mesh.RecalculateBounds();
    }
}