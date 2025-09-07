using UnityEngine;
using PM = ProgramManager;

[ExecuteInEditMode]
public class MaterialInput : MonoBehaviour
{
    [Range(0f, 2f)] public float globalMatBrightnessMultiplier = 1f;
    public CustomMat[] materialInputs;

    void OnValidate() => PM.Instance.doOnSettingsChanged = true;
}