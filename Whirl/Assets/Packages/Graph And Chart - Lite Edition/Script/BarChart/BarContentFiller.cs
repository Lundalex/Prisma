using UnityEngine;
using System.Collections;
using ChartAndGraph;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasBarChart))]
public class BarContentFiller : MonoBehaviour
{
    public float FixedAxisMarign = 40f;
    public float FixedGroupSpacing = 25f;
    public float FixedBarSeperation = 10f;
    // Use this for initialization
    void Start()
    {
    }

    void OnValidate()
    {
        var bar = GetComponent<BarChart>();
        bar.Invalidate();
    }

    public virtual void Match()
    {
        var rect = GetComponent<RectTransform>(); 
        var totalWidth = rect.rect.width;
        var bar = GetComponent<CanvasBarChart>();
        bool Stacked = bar.ViewType == BarChart.BarType.Stacked;
        int columnCount = bar.DataSource.TotalCategories;
        int rowCount = bar.DataSource.TotalGroups;

        int rowLimit = rowCount - 1;
        double groupSize = (totalWidth - FixedAxisMarign * 2 - FixedGroupSpacing * rowLimit) / rowCount;
        double groupSeperation = groupSize + FixedGroupSpacing;
        double barSize = (groupSize - FixedBarSeperation * (columnCount - 1)) / columnCount;
        if (Stacked)
            barSize = groupSize;

        bar.AxisSeperation = FixedAxisMarign;
        if (Stacked)
            bar.BarSeperation = 0;
        else
            bar.BarSeperation = FixedBarSeperation + (float)barSize;

        bar.GroupSeperation = (float)groupSeperation;
        bar.BarSize = (float)barSize;// (float)(RatioBarSize * factor);

    }

    // Update is called once per frame
    void Update()
    {

    }
}