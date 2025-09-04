using UnityEngine;
using PM = ProgramManager;

[ExecuteInEditMode]
public class MaterialInput : MonoBehaviour
{
    public CustomMat[] materialInputs;

    void OnValidate() => PM.Instance.doOnSettingsChanged = true;
}