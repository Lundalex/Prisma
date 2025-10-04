// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.Paraform.Demo
{
[ExecuteAlways]
[RequireComponent(typeof(Toggle))]
public class ColorToggle : MonoBehaviour
{
    public Color colorBackgroundOn  = Color.black;
    public Color colorBackgroundOff = Color.white;
    public Color colorForegroundOn  = Color.white;
    public Color colorForegroundOff = Color.black;

    Toggle          _toggle;
    TextMeshProUGUI _text;

#if UNITY_EDITOR
    void Reset()
    {
        _toggle = GetComponent<Toggle>();
        _text   = GetComponentInChildren<TextMeshProUGUI>();

        if (!_toggle)
            return;

        var background = _toggle.targetGraphic;
        if (_toggle.isOn)
        {
            if (background)
                colorForegroundOn = background.color;
            if (_text)
                colorForegroundOn = _text.color;
        }
        else
        {
            if (background)
                colorForegroundOff = background.color;
            if (_text)
                colorForegroundOff = _text.color;
        }
    }

    void OnValidate()
    {
        if (!_toggle)
            return;

        var background = _toggle.targetGraphic;

        if (background) background.color = _toggle.isOn ? colorBackgroundOn : colorBackgroundOff;
        if (_text) _text.color           = _toggle.isOn ? colorForegroundOn : colorForegroundOff;
    }
#endif

    void OnEnable()
    {
        _toggle = GetComponent<Toggle>();
        _text   = GetComponentInChildren<TextMeshProUGUI>();

        _toggle.onValueChanged.AddListener(ChangeColor);
    }

    void ChangeColor(bool isOn)
    {
        if (_toggle.targetGraphic) _toggle.targetGraphic.color = isOn ? colorBackgroundOn : colorBackgroundOff;
        if (_text) _text.color                                 = isOn ? colorForegroundOn : colorForegroundOff;
    }

    void OnDisable()
    {
        _toggle.onValueChanged.RemoveListener(ChangeColor);
    }
}
}
