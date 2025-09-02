using UnityEngine;
using PM = ProgramManager;

[ExecuteInEditMode]
public class MaterialInput : MonoBehaviour
{
    public MatInput[] materialInputs;

    void OnValidate() => PM.Instance.doOnSettingsChanged = true;
}