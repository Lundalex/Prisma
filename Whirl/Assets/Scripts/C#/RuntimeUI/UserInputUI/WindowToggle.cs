using UnityEngine;

[ExecuteInEditMode]
public class WindowToggle : MonoBehaviour
{
    public bool A_B_Active;
    public GameObject A;
    public GameObject B;

#if UNITY_EDITOR
    void Update() => Apply();

    void Apply()
    {
        if (!Application.isPlaying)
        {
            if (A && A.activeSelf != A_B_Active) A.SetActive(A_B_Active);
            if (B && B.activeSelf != !A_B_Active) B.SetActive(!A_B_Active);
        }
    }
#endif
}