using UnityEngine;

[ExecuteInEditMode]
public class SideTask : Task
{
    [Header("Stretch Targets")]
    public RectTransform singleLineStretchTarget;
    public RectTransform multiLineStretchTarget;

    public override void SetWindowByTaskType(bool multiLine_usesA)
    {
        base.SetWindowByTaskType(multiLine_usesA);

        bool isMulti = !multiLine_usesA; // invert bool as crude fix.

        if (singleLineStretchTarget != null)
            singleLineStretchTarget.gameObject.SetActive(!isMulti);

        if (multiLineStretchTarget != null)
            multiLineStretchTarget.gameObject.SetActive(isMulti);
    }
}