using UnityEngine;

[ExecuteInEditMode]
public class SideTask : Task
{
    [Header("Stretch Targets (Local to this SideTask)")]
    public RectTransform stretchTarget;
    public RectTransform altStretchTarget;
}