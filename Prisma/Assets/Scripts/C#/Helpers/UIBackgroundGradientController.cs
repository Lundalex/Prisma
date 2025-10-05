using UnityEngine;
using Michsky.MUIP;

[ExecuteAlways]
[DisallowMultipleComponent]
public class UIBackgroundGradientController : MonoBehaviour
{
    [SerializeField] UIManager uiManager;

    UIGradient _gradient;

    void Reset()               => EnsureGradient();
    void OnEnable()            { EnsureGradient(); Apply(); }
    void OnValidate()          { if (!Application.isPlaying) { EnsureGradient(); Apply(); } }
    void Update()              => Apply();

    void EnsureGradient()
    {
        if (_gradient == null) _gradient = GetComponent<UIGradient>();
        if (_gradient == null) _gradient = gameObject.AddComponent<UIGradient>();
    }

    void Apply()
    {
        if (!uiManager) return;
        EnsureGradient();
        _gradient.enabled = uiManager.ActiveColorPalette.doUseBackgroundGradient;
    }
}
