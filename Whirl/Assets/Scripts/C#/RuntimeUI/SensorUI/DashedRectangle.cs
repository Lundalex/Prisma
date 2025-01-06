using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(LineRenderer))]
public class DashedRectangle : MonoBehaviour
{
    // Serialized fields
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private float width = 5f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float cornerRadius = 0.5f;
    [SerializeField] private int cornerSegments = 10;
    
    // Non-serialized fields
    [NonSerialized] public SensorUI sensorUI;

    // Private references
    private LineRenderer lineRenderer;

    // Private variables
    private bool activeStatus = false;
    private Vector3 scale = Vector3.one;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (ProgramManager.Instance.programStarted) Initialize();
    }
#endif

    public void Initialize()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        
        // Generate positions
        List<Vector3> positions = GenerateRoundedRectanglePositions(width * scale.x, height * scale.y, cornerRadius, cornerSegments);
        lineRenderer.positionCount = positions.Count;
        lineRenderer.SetPositions(positions.ToArray());
    }

    public void SetPosition(Vector2 newPosition)
    {
        if (newPosition != rectTransform.anchoredPosition)
        {
            rectTransform.anchoredPosition = newPosition;
        }
    }

    public void SetScale(Vector3 newScale)
    {
        if (newScale != scale)
        {
            scale = newScale;
            Initialize();
        }
    }

    public void SetActive(bool newActiveStatus)
    {
        if (activeStatus != newActiveStatus)
        {
            activeStatus = newActiveStatus;
            gameObject.SetActive(activeStatus);
        }
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        Initialize();
    }

    private List<Vector3> GenerateRoundedRectanglePositions(float width, float height, float radius, int segments)
    {
        List<Vector3> positions = new();

        // Clamp radius to avoid overlapping corners
        float maxRadius = Mathf.Min(width, height) / 2f;
        radius = Mathf.Clamp(radius, 0f, maxRadius);

        Vector2[] corners = new Vector2[4];
        corners[0] = new Vector2(-width / 2 + radius, -height / 2 + radius); // Bottom Left
        corners[1] = new Vector2(width / 2 - radius, -height / 2 + radius);  // Bottom Right
        corners[2] = new Vector2(width / 2 - radius, height / 2 - radius);   // Top Right
        corners[3] = new Vector2(-width / 2 + radius, height / 2 - radius);  // Top Left

        float[] angles = { 180f, 270f, 0f, 90f };
        for (int i = 0; i < 4; i++)
        {
            Vector2 center = corners[i];
            float startAngle = angles[i];
            float endAngle = startAngle + 90f;

            // Generate points for the corner arc
            for (int j = 0; j <= segments; j++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle, (float)j / segments);
                float rad = Mathf.Deg2Rad * angle;
                Vector2 point = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                positions.Add(point);
            }
        }

        return positions;
    }
}
