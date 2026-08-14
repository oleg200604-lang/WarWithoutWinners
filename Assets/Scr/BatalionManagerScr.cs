using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatalionManagerScr : MonoBehaviour
{
    public int teamID;

    public int[] teamEnemyID;
    public int[] teamAllyID;
    public BattalionScr selectBattalion;

    public List<Regiment> regiment;

    // Який наказ (0, 1, 2) зараз редагується клавішами 1/2/3
    private int commandDuty;

    [Header("Індикатор дальності — заливка (без контура)")]
    public MeshFilter rangeFillMeshFilter;
    public Color rangeFillColor = new Color(0.2f, 1f, 0.3f, 0.25f);
    public int rangeSegments = 48;
    private Mesh rangeFillMesh;
    private MeshRenderer rangeFillMeshRenderer;

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            commandDuty = 0;
            print("Наказ 1");
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            commandDuty = 1;
            print("Наказ 2");
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            commandDuty = 2;
            print("Наказ 3");
        }

        if (Mouse.current.rightButton.wasPressedThisFrame && selectBattalion != null)
        {
            Vector3 mousePosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
            worldPosition.z = 0;

            Vector3 origin = selectBattalion.GetOrderOrigin(commandDuty);
            float maxRange = selectBattalion.GetRemainingRange(commandDuty);
            float dist = Vector3.Distance(origin, worldPosition);

            if (dist <= maxRange)
            {
                selectBattalion.SetMoveOrder(commandDuty, worldPosition);
            }
            else
            {
                print("Точка поза межами дальності — наказ не встановлено");
            }
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && selectBattalion != null)
        {
            selectBattalion.isRun = true;
        }

        UpdateRangeFill();
    }

    private void UpdateRangeFill()
    {
        if (rangeFillMeshFilter == null)
            return;

        if (rangeFillMesh == null)
        {
            rangeFillMesh = new Mesh();
            rangeFillMesh.name = "RangeFillMesh";
            rangeFillMeshFilter.mesh = rangeFillMesh;
        }
        if (rangeFillMeshRenderer == null)
            rangeFillMeshRenderer = rangeFillMeshFilter.GetComponent<MeshRenderer>();

        bool show = selectBattalion != null;

        if (!show)
        {
            rangeFillMesh.Clear();
            if (rangeFillMeshRenderer != null)
                rangeFillMeshRenderer.enabled = false;
            return;
        }

        Vector3 origin = selectBattalion.GetOrderOrigin(commandDuty);
        float radius = selectBattalion.GetRemainingRange(commandDuty);

        if (radius <= 0f)
        {
            rangeFillMesh.Clear();
            if (rangeFillMeshRenderer != null)
                rangeFillMeshRenderer.enabled = false;
            return;
        }

        if (rangeFillMeshRenderer != null)
            rangeFillMeshRenderer.enabled = true;

        Vector3[] vertices = new Vector3[rangeSegments + 1];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[rangeSegments * 3];

        Transform meshTransform = rangeFillMeshFilter.transform;
        vertices[0] = meshTransform.InverseTransformPoint(origin);
        colors[0] = rangeFillColor;

        for (int i = 0; i < rangeSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / rangeSegments;
            Vector3 worldPoint = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            vertices[i + 1] = meshTransform.InverseTransformPoint(worldPoint);
            colors[i + 1] = rangeFillColor;
        }

        for (int i = 0; i < rangeSegments; i++)
        {
            int next = (i + 1) % rangeSegments;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        rangeFillMesh.Clear();
        rangeFillMesh.vertices = vertices;
        rangeFillMesh.colors = colors;
        rangeFillMesh.triangles = triangles;
    }
}
[System.Serializable]
public class Regiment
{
    public BattalionScr[] battalions;
}