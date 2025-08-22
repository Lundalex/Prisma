using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InvertedButton : MonoBehaviour
{
    [Tooltip("Invoked when the user clicks anywhere EXCEPT on this object.")]
    public UnityEvent onOutsideClick;

    RectTransform _rt;
    GraphicRaycaster _raycaster;
    EventSystem _eventSystem;
    Collider _col3D;
    Collider2D _col2D;
    Camera _cam;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (_rt) _raycaster = GetComponentInParent<GraphicRaycaster>();
        _eventSystem = EventSystem.current;

        _col3D = GetComponent<Collider>();
        _col2D = GetComponent<Collider2D>();
        _cam = Camera.main;
    }

    void Update()
    {
        // Left mouse only
        if (!Input.GetMouseButtonDown(0)) return;

        bool pointerOnThis = false;

        if (_rt && _raycaster && _eventSystem)
        {
            pointerOnThis = IsPointerOverThisUI();
        }
        else if (_col3D || _col2D)
        {
            pointerOnThis = IsPointerOverThisWorldObject();
        }

        if (!pointerOnThis)
            onOutsideClick?.Invoke();
    }

    bool IsPointerOverThisUI()
    {
        var ped = new PointerEventData(_eventSystem) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        _raycaster.Raycast(ped, results);

        foreach (var r in results)
        {
            if (r.gameObject == gameObject) return true;
            if (_rt && r.gameObject.transform.IsChildOf(_rt)) return true;
        }
        return false;
    }

    bool IsPointerOverThisWorldObject()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        var ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (_col3D)
        {
            var hits = Physics.RaycastAll(ray, float.MaxValue);
            if (hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits[0].collider == _col3D;
        }

        if (_col2D)
        {
            var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            if (!hit.collider) return false;
            return hit.collider == _col2D;
        }

        return false;
    }
}