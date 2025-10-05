using UnityEngine;

[ExecuteInEditMode]
public class WindowToggle : MonoBehaviour
{
    public bool A_B_Active;
    public GameObject A;
    public GameObject B;

    public float delaySeconds = 0f;

    private Coroutine pendingSwitch;

    public void SetModeA(bool aActive)
    {
        if (!Application.isPlaying || delaySeconds <= 0f)
        {
            A_B_Active = aActive;
            ApplyNow();
            return;
        }

        if (pendingSwitch != null)
        {
            StopCoroutine(pendingSwitch);
            pendingSwitch = null;
        }

        pendingSwitch = StartCoroutine(DelayAndApply(aActive));
    }

    private System.Collections.IEnumerator DelayAndApply(bool aActive)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        A_B_Active = aActive;
        ApplyNow();
        pendingSwitch = null;
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

    private void OnDisable()
    {
        if (pendingSwitch != null)
        {
            StopCoroutine(pendingSwitch);
            pendingSwitch = null;
        }
    }
}