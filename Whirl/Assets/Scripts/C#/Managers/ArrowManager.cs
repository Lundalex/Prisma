using System;
using UnityEngine;

public class ArrowManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject uiArrowPrefab;
    [SerializeField] private Transform arrowContainerTransform;

    // Private
    private GameObject[] arrowObjects;

    private void Awake() => arrowObjects = new GameObject[0];

    public UIArrow CreateArrow(
        Vector2 center,
        float radius,
        float displayBoxScale,
        float value,
        int numDecimals,
        float rotation,
        string unit,
        float baseWidth,
        float baseLength,
        float hatSize,
        float outlineGap,
        Color minOutlineColor,
        Color maxOutlineColor,
        Color minBodyColor,
        Color maxBodyColor,
        Color displayBoxColor)
    {
        GameObject arrowObject = Instantiate(uiArrowPrefab, this.transform, false);
        arrowObject.transform.SetParent(arrowContainerTransform, false);

        UIArrow uiArrow = arrowObject.GetComponent<UIArrow>();
        uiArrow.SetConfig(center, radius, displayBoxScale, value, numDecimals, rotation, unit, baseWidth, baseLength, hatSize, outlineGap, minOutlineColor, maxOutlineColor, minBodyColor, maxBodyColor, displayBoxColor);

        AddArrowObject(arrowObject);

        return uiArrow;
    }

    public UIArrow CreateArrow(GameObject uiArrowPrefab)
    {
        GameObject arrowObject = Instantiate(uiArrowPrefab, this.transform, false);
        arrowObject.transform.SetParent(arrowContainerTransform, false);

        AddArrowObject(arrowObject);

        return arrowObject.GetComponent<UIArrow>();
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
        float displayBoxScale,
        float value,
        int numDecimals,
        float rotation,
        string unit,
        float baseWidth,
        float baseLength,
        float hatSize,
        float outlineGap,
        Color minOutlineColor,
        Color maxOutlineColor,
        Color minBodyColor,
        Color maxBodyColor,
        Color displayBoxColor)
    {
        if (arrowObjects == null || index < 0 || index >= arrowObjects.Length) return;

        if (arrowObjects[index].TryGetComponent<UIArrow>(out var uiArrow))
        {
            uiArrow.SetConfig(center, radius, displayBoxScale, value, numDecimals, rotation, unit, baseWidth, baseLength, hatSize, outlineGap, minOutlineColor, maxOutlineColor, minBodyColor, maxBodyColor, displayBoxColor);
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
    
    public void ClearAllArrows(bool immediate = true)
    {
        if (arrowContainerTransform != null)
        {
            for (int i = arrowContainerTransform.childCount - 1; i >= 0; i--)
            {
                var go = arrowContainerTransform.GetChild(i).gameObject;
                if (immediate) DestroyImmediate(go); else Destroy(go);
            }
        }

        arrowObjects = new GameObject[0];
    }
}
