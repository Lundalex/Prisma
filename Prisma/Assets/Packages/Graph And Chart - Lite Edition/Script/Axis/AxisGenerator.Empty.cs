using UnityEngine;
using System.Collections;
namespace ChartAndGraph.Axis
{
    public partial class AxisGenerator : MonoBehaviour,IAxisGenerator
    {
        partial void InnerFixLabels(AnyChart parent);
        partial void InnerSetAxis(double scrollOffset, AnyChart parent, AxisBase axis, ChartOrientation axisOrientation, int divType);

        public void FixLabels(AnyChart parent)
        {
            InnerFixLabels(parent);
        }

        public void SetAxis(double scrollOffset, AnyChart parent, AxisBase axis, ChartOrientation axisOrientation, int divType)
        {
            InnerSetAxis(scrollOffset, parent, axis, axisOrientation, divType);
        }

        public UnityEngine.Object This()
        {
            return this;
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

    }
}
 