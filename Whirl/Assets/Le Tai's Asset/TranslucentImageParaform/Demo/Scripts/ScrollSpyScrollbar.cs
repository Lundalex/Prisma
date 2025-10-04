// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections.Generic;
using LeTai.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LeTai.Paraform.Demo
{
public class ScrollSpyScrollbar : MonoBehaviour
{
    public Toggle           markerPrefab;
    public TinyTween.Spring spring = TinyTween.Spring.DEFAULT;
    public float            bias   = .1f;

    private          ScrollRect            _scrollRect;
    private readonly List<float>           _snapPoints = new();
    private readonly List<Toggle>          _toggles    = new();
    private readonly List<ScrollSpyMarker> _markers    = new();

    private void Start()
    {
        _scrollRect = GetComponent<ScrollRect>();
        Canvas.ForceUpdateCanvases();

        var content         = _scrollRect.content;
        var scrollbar       = _scrollRect.verticalScrollbar;
        var markerContainer = (RectTransform)scrollbar.handleRect.parent;

        int count          = content.childCount;
        var contentHeight  = content.rect.height;
        var viewportHeight = _scrollRect.viewport.rect.height;

        for (int iChild = 0, iMarker = 0; iChild < count; iChild++)
        {
            var child = (RectTransform)content.GetChild(iChild);
            if (!child.gameObject.activeSelf)
                continue;

            var marker = child.GetComponent<ScrollSpyMarker>();
            if (!marker)
                continue;

            _markers.Add(marker);

            float topOffset  = -child.anchoredPosition.y;
            float normalized = 1f - Mathf.Clamp01(topOffset / (contentHeight - viewportHeight));
            _snapPoints.Add(normalized);

            var toggle = Instantiate(markerPrefab, markerContainer);
            toggle.interactable = false;
            _toggles.Add(toggle);

            var toggleRt = (RectTransform)toggle.transform;

            toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(.5f, normalized);

            var ev = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };

            int scrollIndex = iMarker;
            ev.callback.AddListener(_ => ScrollTo(scrollIndex));
            var et = toggle.gameObject.AddComponent<EventTrigger>();
            et.triggers.Add(ev);

            iMarker++;
        }

        _scrollRect.onValueChanged.AddListener(_ => UpdateHighlight());
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        float pos         = _scrollRect.verticalNormalizedPosition + bias;
        int   activeIndex = 0;
        float minDist     = Mathf.Abs(pos - _snapPoints[0]);

        for (int i = 1; i < _snapPoints.Count; i++)
        {
            float dist = Mathf.Abs(pos - _snapPoints[i]);
            if (dist < minDist)
            {
                minDist     = dist;
                activeIndex = i;
            }
        }

        for (int i = 0; i < _toggles.Count; i++)
        {
            bool isActive = i == activeIndex;
            _toggles[i].isOn = isActive;
            _markers[i].SetActive(isActive);
        }
    }

    private void ScrollTo(int index)
    {
        TinyTween.Animate(_scrollRect, _scrollRect.verticalNormalizedPosition, _snapPoints[index],
                          static (ctx, next) => ctx.verticalNormalizedPosition = next,
                          spring);
        UpdateHighlight();
    }
}
}
