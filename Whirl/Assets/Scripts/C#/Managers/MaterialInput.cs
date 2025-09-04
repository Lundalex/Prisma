using UnityEngine;
using PM = ProgramManager;

[ExecuteInEditMode]
public class MaterialInput : MonoBehaviour
{
    public BaseMat[] materialInputs;

    void OnValidate() => PM.Instance.doOnSettingsChanged = true;
}