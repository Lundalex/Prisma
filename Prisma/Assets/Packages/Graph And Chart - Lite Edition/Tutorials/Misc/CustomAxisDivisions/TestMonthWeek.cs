using ChartAndGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMonthWeek : MonoBehaviour
{
    public GraphChart chart;

    // Start is called before the first frame update
    void Start()
    {
        chart.DataSource.ClearCategory("Player 1");
        var now = DateTime.Now;
        for(int i=0; i<60; i++)
            chart.DataSource.AddPointToCategory("Player 1", now.AddMonths(i), UnityEngine.Random.Range(0f, 10f));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
