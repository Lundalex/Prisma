using UnityEngine;
using PM = ProgramManager;

public class MaterialInput : MonoBehaviour
{
    public MatInput[] materialInputs;

    public void OnValidate() => PM.Instance.doOnSettingsChanged = true;
}