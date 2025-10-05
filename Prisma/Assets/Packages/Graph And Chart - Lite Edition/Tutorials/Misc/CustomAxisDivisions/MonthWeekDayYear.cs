using ChartAndGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonthWeekDayYear : MonoBehaviour
{
    
    public GapEnum Gap;
    double mStartPosition = 0;
    double mEndPostion = 0;
    GapEnum? mCurrent = null;
    void Start()
    {

    }
    private void OnValidate()
    {

    }
    void SetConstantGap(int days)
    {
        var axis = GetComponent<HorizontalAxis>();
        if (axis == null)
            return;
        if(axis.SubDivisions.Total != 0)
            axis.SubDivisions.Total = 0;
        if(axis.MainDivisions.Messure != ChartDivisionInfo.DivisionMessure.DataUnits)
            axis.MainDivisions.Messure = ChartDivisionInfo.DivisionMessure.DataUnits;
        float val = (float)(days * TimeSpan.FromDays(1).TotalSeconds);
        if (axis.MainDivisions.UnitsPerDivision != val)
            axis.MainDivisions.UnitsPerDivision = val;
    }
    void FixDivisions()
    {

    }
    void Regenrate()
    {
        var chart = GetComponent<ScrollableAxisChart>();
        var axis = GetComponent<HorizontalAxis>();
        if (chart == null || axis == null)
            return;
        if (axis.SubDivisions.Total != 0)
            axis.SubDivisions.Total = 0;
        if (axis.MainDivisions.Messure != ChartDivisionInfo.DivisionMessure.TotalDivisions)
            axis.MainDivisions.Messure = ChartDivisionInfo.DivisionMessure.TotalDivisions;
        if (axis.MainDivisions.Total != 1)
            axis.MainDivisions.Total = 1;
        chart.ScrollableData.RestoreDataValues(0);
        double startPosition = chart.ScrollableData.HorizontalViewOrigin + chart.HorizontalScrolling;
        double endPosition = chart.ScrollableData.HorizontalViewSize + startPosition;

        if (endPosition < startPosition)
        {
            double tmp = startPosition;
            startPosition = endPosition;
            endPosition = tmp;
        }
        double half = Math.Abs(chart.ScrollableData.HorizontalViewSize * 0.5f);
        mStartPosition = startPosition - half;
        mEndPostion = endPosition + half;
        if (Gap == GapEnum.Month)
            RegenrateMonth();
        else if (Gap == GapEnum.Year)
            RegenarateYear();
    }
    void RegenarateYear()
    {
        var chart = GetComponent<ScrollableAxisChart>();
        if (chart == null)
            return;
        chart.ClearHorizontalCustomDivisions();
        var startDate = ChartDateUtility.ValueToDate(mStartPosition);
        var endDate = ChartDateUtility.ValueToDate(mEndPostion);
        var origin = ChartDateUtility.ValueToDate(chart.ScrollableData.HorizontalViewOrigin);
        int yearGap = startDate.Year - origin.Year;
        DateTime current = origin.AddYears(yearGap);
        while (current < endDate)
        {
            chart.AddHorizontalAxisDivision(ChartDateUtility.DateToValue(current));
            yearGap++;
            current = origin.AddYears(yearGap);
        }
    }
    void RegenrateMonth()
    {
        var chart = GetComponent<ScrollableAxisChart>();
        if (chart == null)
            return;
        
        chart.ClearHorizontalCustomDivisions();
        var startDate = ChartDateUtility.ValueToDate(mStartPosition);
        var endDate = ChartDateUtility.ValueToDate(mEndPostion);
        var origin = ChartDateUtility.ValueToDate(chart.ScrollableData.HorizontalViewOrigin);
        int yearGap = startDate.Year - origin.Year;
        int monthGap = startDate.AddYears(yearGap).Month - origin.Month;
        DateTime current = origin.AddYears(yearGap).AddMonths(monthGap);
        while(current < endDate)
        {
            chart.AddHorizontalAxisDivision(ChartDateUtility.DateToValue(current));
            monthGap++;
            current = origin.AddYears(yearGap).AddMonths(monthGap);
        }
    }
    bool IsViewInside()
    {
        var chart = GetComponent<ScrollableAxisChart>();
        if (chart == null)
            return false;
        double startPosition = chart.ScrollableData.HorizontalViewOrigin + chart.HorizontalScrolling;
        double endPosition = chart.ScrollableData.HorizontalViewSize + startPosition;

        if (endPosition < startPosition)
        {
            double tmp = startPosition;
            startPosition = endPosition;
            endPosition = tmp;
        }
        if (startPosition < mStartPosition)
            return false;
        if (endPosition > mEndPostion)
            return false;
        return true;

    }
    void CheckGenerate()
    {
        switch(Gap)
        {
            case GapEnum.Day:
                SetConstantGap(1);
                break;
            case GapEnum.Week:
                SetConstantGap(7);
                break;
            case GapEnum.Month:
                if(IsViewInside() == false || mCurrent.HasValue == false || mCurrent.Value != GapEnum.Month)
                    Regenrate();
                break;
            case GapEnum.Year:
                if (IsViewInside() == false || mCurrent.HasValue == false || mCurrent.Value != GapEnum.Year)
                    Regenrate();
                break;
        }
        mCurrent = Gap;
    }
    void Update()
    {
        CheckGenerate();
    }
}
