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

        _isMulti = multiLine_usesA;

        if (singleLineStretchTarget != null) singleLineStretchTarget.gameObject.SetActive(!_isMulti);
        if (multiLineStretchTarget != null) multiLineStretchTarget.gameObject.SetActive(_isMulti);
    }

    public override void PlayCorrectMark()
    {
        var icon = _isMulti ? multiLineCorrectIcon : singleLineCorrectIcon;
        if (icon == null) icon = multiLineCorrectIcon ?? singleLineCorrectIcon;
        if (icon != null) icon.Play();
        else base.PlayCorrectMark();
    }

    public void OpenFullscreenView()
    {
        TaskManager?.OpenFullscreenView();
    }
}