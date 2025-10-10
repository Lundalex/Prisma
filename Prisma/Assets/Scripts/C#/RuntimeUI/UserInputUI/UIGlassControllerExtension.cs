using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UIGlassControllerExtension : MonoBehaviour
{
    public RectTransform glassContainerReference;
    public GameObject GlassUI;
    public List<Image> NonGlassyUI = new();


    [Header("Glass Padding")]
    public float paddingX = 0f;
    public float paddingY = 0f;

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

        var refRect = glassContainerReference;

        // Mirror anchors/pivot from the reference
        glassRect.anchorMin = refRect.anchorMin;
        glassRect.anchorMax = refRect.anchorMax;
        glassRect.pivot = refRect.pivot;

        // Start from the reference rect
        glassRect.anchoredPosition = refRect.anchoredPosition;
        glassRect.sizeDelta = refRect.sizeDelta;

        // Apply padding (equal on all sides)
        if (paddingX != 0f || paddingY != 0f)
        {
            Vector2 extra = new(paddingX * 2f, paddingY * 2f);
            glassRect.sizeDelta += extra;

            Vector2 p = refRect.pivot;
            Vector2 shift = new((p.x - 0.5f) * extra.x, (p.y - 0.5f) * extra.y);
            glassRect.anchoredPosition += shift;
        }
    }
}