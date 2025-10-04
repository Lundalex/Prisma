using UnityEngine;

public class FixedRectGlobalScale : MonoBehaviour
{
    public float globalScale = 1f;
    void Update()
    {
        // Calculate the scale relative to the parent’s world scale
        Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(globalScale / parentScale.x,
                                           globalScale / parentScale.y,
                                           globalScale / parentScale.z);
    }
}