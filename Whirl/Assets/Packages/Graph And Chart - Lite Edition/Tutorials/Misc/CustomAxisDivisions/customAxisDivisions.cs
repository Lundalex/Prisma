using ChartAndGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine; 

public class customAxisDivisions : MonoBehaviour
{
    public GraphChart chart;
    // Start is called before the first frame update
    void Start()
    {
        chart.DataSource.ClearCategory("Player 1");
        DateTime now = DateTime.Now; 
        for (int i = 0; i < 36; i++)
            chart.DataSource.AddPointToCategory("Player 1", now + TimeSpan.FromDays(i * 10), UnityEngine.Random.Range(0f, 10f));
        DateTime month = now;
        chart.ClearHorizontalCustomDivisions();
        for (int i = 0; i < 12; i++)
        {
            chart.AddHorizontalAxisDivision(ChartDateUtility.DateToValue(month));
            chart.AddHorizontalAxisDivision(ChartDateUtility.DateToValue(month.AddDays(15)),true);
            month = month.AddMonths(1);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
