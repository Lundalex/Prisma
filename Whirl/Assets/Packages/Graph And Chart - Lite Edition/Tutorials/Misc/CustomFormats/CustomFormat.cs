using ChartAndGraph;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomFormat : MonoBehaviour
{
    public GraphChart chart;
    // Start is called before the first frame update
    void Start()
    {
        chart.CustomNumberFormat = (nubmer, fractionDigits) =>
        {
            return (int)(nubmer / 1000) + "K";
        };
        chart.CustomDateTimeFormat = (date) => { return date.ToString("MMM"); };
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
