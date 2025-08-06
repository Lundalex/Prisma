using UnityEngine;
using UnityEngine.EventSystems;
using Resources2;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class MultiContainerDragHandle :
    MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public MultiContainer multiContainer;

    Vector2 _pointerStart;
    float   _containerStart;
    float   _worldPerPixel;

    Coroutine _hoverRoutine;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (multiContainer) multiContainer.SwitchViewMode();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (multiContainer == null) return;

        multiContainer.BeginDrag();

        _pointerStart   = eventData.position;
        _containerStart = multiContainer.transform.localPosition.x;

        (float expX, float minX) = multiContainer.GetMinExpView();

        Transform parent = multiContainer.transform.parent;
        Vector3 worldExp = parent.TransformPoint(new Vector3(expX, 0f, 0f));
        Vector3 worldMin = parent.TransformPoint(new Vector3(minX, 0f, 0f));

        Camera cam   = eventData.pressEventCamera;
        float  pxExp = RectTransformUtility.WorldToScreenPoint(cam, worldExp).x;
        float  pxMin = RectTransformUtility.WorldToScreenPoint(cam, worldMin).x;

        float screenRange = Mathf.Max(Mathf.Abs(pxExp - pxMin), 1f);
        float worldRange  = Mathf.Abs(expX - minX);

        _worldPerPixel = worldRange / screenRange;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (multiContainer == null) return;

        float deltaPixels = eventData.position.x - _pointerStart.x;
        float newX        = _containerStart + deltaPixels * _worldPerPixel;

        multiContainer.SetDraggedPosition(newX);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (multiContainer) multiContainer.EndDragSnap();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartHoverScale(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartHoverScale(false);
    }

    void StartHoverScale(bool enlarge)
    {
        if (_hoverRoutine != null) StopCoroutine(_hoverRoutine);
        _hoverRoutine = StartCoroutine(HoverScaleRoutine(enlarge ? multiContainer.hoverScaleMultiplier : 1f));
    }

    IEnumerator HoverScaleRoutine(float targetMult)
    {
        float start = transform.localScale.x;
        float end   = multiContainer.handleBaseScale * targetMult;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / multiContainer.hoverDuration;
            float e = Lerp.Evaluate((Lerp.Curve)multiContainer.hoverCurve, t);
            float scale1D = Mathf.Lerp(start, end, e);
            transform.localScale = Utils.FloatToVector3(scale1D);
            yield return null;
        }
        transform.localScale = Utils.FloatToVector3(end);
    }
}