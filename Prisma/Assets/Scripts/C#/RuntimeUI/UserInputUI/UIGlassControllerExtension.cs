using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utilities.Extensions;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UIGlassControllerExtension : MonoBehaviour
{
    public RectTransform glassContainerReference;
    public GameObject GlassUI;
    public List<Image> NonGlassyUI = new();

    bool _isActive;
    bool _alignQueued;

    public void SetGlassyUIActive(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;
        ApplyState();
        if (_isActive) RequestAlign();
    }

    void OnEnable()
    {
        ApplyState();
        if (_isActive) RequestAlign();
    }

    void OnValidate()
    {
        ApplyState();
        if (_isActive) RequestAlign();
    }

    void LateUpdate()
    {
        if (_alignQueued)
        {
            _alignQueued = false;
            AlignGlassyToContainer();
        }
    }

    void ApplyState()
    {
        if (GlassUI) GlassUI.SetActive(_isActive);

        if (NonGlassyUI != null)
        {
            for (int i = 0; i < NonGlassyUI.Count; i++)
            {
                Image img = NonGlassyUI[i];
                if (img) img.enabled = !_isActive;
                // img.SetActive(true);
            }
        }
    }

    void RequestAlign()
    {
        if (Application.isPlaying)
        {
            _alignQueued = true;
        }
#if UNITY_EDITOR
        else
        {
            EditorApplication.delayCall += AlignGlassyToContainer;
        }
#endif
    }

    void AlignGlassyToContainer()
    {
        if (!GlassUI || !glassContainerReference) return;

        var glassRect = GlassUI.GetComponent<RectTransform>();
        if (!glassRect) return;

        glassRect.anchorMin = glassContainerReference.anchorMin;
        glassRect.anchorMax = glassContainerReference.anchorMax;
        glassRect.pivot = glassContainerReference.pivot;
        glassRect.anchoredPosition = glassContainerReference.anchoredPosition;
        glassRect.sizeDelta = glassContainerReference.sizeDelta;
    }
}