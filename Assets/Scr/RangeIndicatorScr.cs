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
            CommandType commandType = batalionManager.commandType;

            if (selected == null)
            {
                Hide();
                HideRayFan();
                return;
            }

            int slot = batalionManager.CommandDuty;
            Vector3 origin = selected.GetOrderOrigin(slot);

        if (commandType == CommandType.Move)
        {
            HideRayFan();

            float remainingSpeed = selected.GetRemainingRange(slot);

            if (remainingSpeed <= 0f)
            {
                Hide();
                return;
            }

            Show();
            BuildMoveArea(selected, origin, remainingSpeed, fillColor);
        }
        else if (commandType == CommandType.Attack || commandType == CommandType.Defend)
        {
            bool isDefend = commandType == CommandType.Defend;

            // Дальність розраховується від позиції,
            // де батальйон буде виконувати цей наказ.
            // Це важливо для 2-го та 3-го наказу в черзі.
            float maxRange = selected.GetEffectiveAttackRange(origin);

            if (maxRange <= 0f)
            {
                Hide();
                HideRayFan();
                return;
            }

            Show();

            BuildCircle(
                origin,
                maxRange,
                isDefend ? defendFillColor : attackFillColor
            );

            ShowRayFan(
                selected,
                origin,
                maxRange,
                isDefend ? defendRayColor : rayColor
            );
        }
        else
        {
            // None — жодного прев'ю.
            Hide();
            HideRayFan();
        }
        }

        private void ShowRayFan(BattalionScr selected, Vector3 origin, float range, Color color)
        {
            LineRenderer rayFan = EnsureRayFan();
            if (rayFan == null)
                return;

            Vector3 direction = GetAimDirection(origin);

            int rayCount = 31;
            float coneAngle = 90f;
            if (selected.attackSystem != null)
            {
                rayCount = Mathf.Max(1, selected.attackSystem.RayCount);
                coneAngle = selected.attackSystem.ConeAngle;
            }

            float startAngle = -coneAngle * 0.5f;
            float angleStep = rayCount > 1 ? coneAngle / (rayCount - 1) : 0f;

            // Один LineRenderer малює всі промені разом: origin -> кінець
            // променя -> origin -> кінець наступного -> ... Пряме повернення в
            // origin між променями не створює зайвих діагоналей, лише "зірку"
            // з одної точки.
            Vector3[] points = new Vector3[rayCount * 2];
            for (int i = 0; i < rayCount; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector3 rayDir = Quaternion.Euler(0f, 0f, angle) * direction;

                points[i * 2] = origin;
                points[i * 2 + 1] = origin + rayDir * range;
            }

            rayFan.positionCount = points.Length;
            rayFan.SetPositions(points);
            rayFan.startColor = color;
            rayFan.endColor = color;
            rayFan.enabled = true;
        }

        /// <summary>
        /// rayFanLineRenderer раніше треба було вручну перетягнути в
        /// інспекторі — якщо цього не зробити, промені просто мовчки не
        /// малювались. Тепер, якщо поле порожнє, компонент створює собі
        /// LineRenderer сам, на дочірньому об'єкті, з базовим матеріалом
        /// (Sprites/Default, він є в будь-якому проєкті).
        /// </summary>
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

        /// <summary>Напрямок прицілювання — до курсора миші (як і в BatalionManagerScr.Attack()).</summary>
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

            // Подвійна намотка трикутників (лицева + зворотна), щоб меш було
            // видно незалежно від напрямку камери.
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

    private void BuildMoveArea(
    BattalionScr selected,
    Vector3 origin,
    float remainingSpeed,
    Color color)
    {
        int safeSegments = Mathf.Max(12, segments);

        Vector3[] vertices = new Vector3[safeSegments + 1];
        Color[] colors = new Color[vertices.Length];

        // Подвійні трикутники, як у твоєму BuildCircle:
        // область буде видна з обох сторін.
        int[] triangles = new int[safeSegments * 6];

        Transform t = meshFilter.transform;
        vertices[0] = t.InverseTransformPoint(origin);
        colors[0] = color;

        // Road має множник 0.5, тому батальйон може пройти до Speed × 2.
        // Значення також не може бути меншим за заданий у Inspector запас.
        float testDistance = Mathf.Max(
            0,
            remainingSpeed * 2.05f
        );

        for (int i = 0; i < safeSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / safeSegments;

            Vector3 direction = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f
            );

            // Той самий розрахунок terrain-вартості, що застосовується
            // під час встановлення наказу.
            float reachableDistance = selected.GetReachableDistance(
                origin,
                direction,
                testDistance,
                remainingSpeed
            );

            Vector3 worldPoint =
                origin + direction * reachableDistance;

            vertices[i + 1] =
                t.InverseTransformPoint(worldPoint);

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