using UnityEngine;

[ExecuteInEditMode]
public class WindowToggle : MonoBehaviour
{
    [Tooltip("When true: A active, B inactive. When false: B active, A inactive.")]
    public bool A_B_Active;
    public GameObject A;
    public GameObject B;

    public void SetModeA(bool aActive)
    {
        A_B_Active = aActive;
        ApplyNow();
    }

    public void ApplyNow()
    {
        if (A && A.activeSelf != A_B_Active) A.SetActive(A_B_Active);
        if (B && B.activeSelf != !A_B_Active) B.SetActive(!A_B_Active);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying) ApplyNow();
    }
#endif
}