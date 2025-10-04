// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using LeTai.Asset.TranslucentImage;
using LeTai.Common;
using UnityEngine;

namespace LeTai.Paraform.Demo
{
public class DemoControllerFloater : MonoBehaviour
{
    public RectTransform floater;

    public TinyTween.Spring springFollow = TinyTween.Spring.DEFAULT;

    public TinyTween.Spring springExit = TinyTween.Spring.DEFAULT;

    public RectTransform boundUI;

    RectTransform _parent;
    Camera        _cam;
    Vector2       _restPos;
    bool          _isOffscreen;

    RectTransform canvasRt;
    float         ExitOffset => -canvasRt.rect.height * 1.2f;

    void Start()
    {
        _parent = floater.parent as RectTransform;
        _cam    = boundUI.GetComponentInParent<Canvas>().worldCamera;

        floater.gameObject.SetActive(true);
        _restPos = floater.anchoredPosition;

        floater.GetComponent<TranslucentImage>().raycastTarget = Application.isMobilePlatform;
        
        canvasRt = (RectTransform)GetComponentInParent<Canvas>().transform;
    }

    void Update()
    {
        if (_isOffscreen)
            return;

        Vector2 target = _restPos;
        if (RectTransformUtility.RectangleContainsScreenPoint(boundUI, Input.mousePosition, _cam))
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, Input.mousePosition, _cam,
                                                                        out var mouseLocal))
            {
                target = mouseLocal;
            }
        }

        TinyTween.Move(floater, target, springFollow);
    }

    public void AnimateIn()
    {
        _isOffscreen = false;
        TinyTween.Move(floater, _restPos, springExit);
    }

    public void AnimateOut()
    {
        _isOffscreen = true;
        TinyTween.Move(floater, new Vector2(floater.anchoredPosition.x, _restPos.y + ExitOffset), springExit);
    }
}
}
