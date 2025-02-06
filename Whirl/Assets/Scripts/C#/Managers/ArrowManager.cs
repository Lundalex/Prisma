using System;
using UnityEngine;

public class ArrowManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform arrowContainerTransform;

    // Private
    private GameObject[] arrowObjects;

    private void Awake() => arrowObjects = new GameObject[0];

    public GameObject CreateArrow(
        Vector2 center,
        float radius,
        float rotation,
        float baseWidth,
        float baseLength,
        float hatSize,
        float outlineGap,
        Color outlineColor,
        Color bodyColor)
    {
        GameObject arrowObject = Instantiate(arrowPrefab, this.transform, false);
        arrowObject.transform.SetParent(arrowContainerTransform, false);

        UIArrow uiArrow = arrowObject.AddComponent<UIArrow>();
        uiArrow.SetConfig(center, radius, rotation, baseWidth, baseLength, hatSize, outlineGap, outlineColor, bodyColor);

        AddArrowObject(arrowObject);

        return arrowObject;
    }

    public void RemoveArrow(int index)
    {
        if (arrowObjects == null || index < 0 || index >= arrowObjects.Length) return;
        DestroyImmediate(arrowObjects[index]);

        for (int i = index; i < arrowObjects.Length - 1; i++) arrowObjects[i] = arrowObjects[i + 1];
        Array.Resize(ref arrowObjects, arrowObjects.Length - 1);
    }

    public void UpdateArrow(
        int index,
        Vector2 center,
        float radius,
        float rotation,
        float baseWidth,
        float baseLength,
        float hatSize,
        float outlineGap,
        Color outlineColor,
        Color bodyColor)
    {
        if (arrowObjects == null || index < 0 || index >= arrowObjects.Length) return;

        if (arrowObjects[index].TryGetComponent<UIArrow>(out var uiArrow))
        {
            uiArrow.SetConfig(center, radius, rotation, baseWidth, baseLength, hatSize, outlineGap, outlineColor, bodyColor);
        }
    }

    private void AddArrowObject(GameObject arrowObject)
    {
        int oldSize = arrowObjects.Length;
        int newSize = oldSize + 1;
        Array.Resize(ref arrowObjects, newSize);
        arrowObject.name = "Arrow_" + newSize;
        arrowObjects[oldSize] = arrowObject;
        
    }
}
