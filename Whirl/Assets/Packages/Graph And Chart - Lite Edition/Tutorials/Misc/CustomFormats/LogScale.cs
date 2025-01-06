using ChartAndGraph;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogScale : MonoBehaviour
{
    public GraphChart Chart;
    // Start is called before the first frame update
    void Start()
    {
        var axis = Chart.GetComponent<VerticalAxis>();
        axis.WithEdges = false;
        var arr = new string[] {"100", "1K", "10K", "100K", "1M", "10M", "100M", "1B" ,"10B"};
        for (int i = 0; i < arr.Length; i++)
        {
            Chart.VerticalValueToStringMap[10 + i * 10] = arr[i];
            Chart.VerticalValueToStringMap[-(10 + i * 10)] = "-" + arr[i];
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
