// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using System.Collections;
using LeTai.Asset.TranslucentImage;
using UnityEngine;

namespace LeTai.Paraform.Demo
{
public class DemoControllerZoom : MonoBehaviour
{
    public TranslucentImage target;
    public RectTransform    pivot;
    public AnimationCurve   curve;
    public float            duration       = 1;
    public float            edgeWidthScale = .2f;

    RectTransform _rt;
    Vector3       _restPos;
    Vector2       _restSize;
    float         _restRadius;
    float         _restEdgeWidth;

    DemoControllerPresets.ShapePreset _restPreset;

    void Start()
    {
        _rt = target.GetComponent<RectTransform>();

        _restPreset = FindAnyObjectByType<DemoControllerPresets>().defaultShapePreset;

        _restPos       = _rt.localPosition;
        _restSize      = _rt.sizeDelta;
        _restRadius    = _restPreset.radius;
        _restEdgeWidth = target.paraformConfig.EdgeWidth;
    }

    public void Zoom()
    {
        StopAllCoroutines();
        StartCoroutine(ZoomCoroutine());
    }

    IEnumerator ZoomCoroutine()
    {
        var start = Time.time;

        _rt.localPosition = _restPos;
        _rt.sizeDelta     = _restSize;
        _restPreset.Apply(target);
        target.paraformConfig.EdgeWidth = _restEdgeWidth;

        while (true)
        {
            var progress = (Time.time - start) / duration;
            if (progress > 1)
                progress = 1;

            SetScale(curve.Evaluate(progress));
            yield return null;

            if (progress >= 1)
                break;
        }
    }

    void SetScale(float nextScale)
    {
        var pivotOffset = _restPos - pivot.localPosition;
        _rt.localPosition = pivot.localPosition + pivotOffset * nextScale;
        _rt.sizeDelta     = _restSize * nextScale;

        target.paraformConfig.CornerRadii = _restRadius * nextScale * Vector4.one;
        target.paraformConfig.EdgeWidth   = _restEdgeWidth * (1 + (nextScale - 1) * edgeWidthScale);
    }
}
}
