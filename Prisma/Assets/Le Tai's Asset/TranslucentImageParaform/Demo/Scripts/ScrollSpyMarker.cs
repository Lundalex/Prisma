// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;
using UnityEngine.Events;

namespace LeTai.Paraform.Demo
{
public class ScrollSpyMarker : MonoBehaviour
{
    public UnityEvent onActivated;
    public UnityEvent onDeactivated;

    bool _isActive = false;

    public void SetActive(bool isActive)
    {
        if (isActive == _isActive)
            return;
        _isActive = isActive;

        if (_isActive)
            onActivated?.Invoke();
        else
            onDeactivated?.Invoke();
    }
}
}
