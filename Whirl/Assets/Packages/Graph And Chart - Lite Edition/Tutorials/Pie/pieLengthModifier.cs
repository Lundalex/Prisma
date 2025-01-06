using ChartAndGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pieLengthModifier : MonoBehaviour
{
    static Dictionary<string, float> LengthDictionary = new Dictionary<string, float>();
    static pieLengthModifier()
    {
        LengthDictionary["Category 1"] = 50f;
        LengthDictionary["Category 2"] = 100f;
        LengthDictionary["Category 3"] = 150f;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var pie = GetComponent<PieInfo>();
        try
        {
            pie.pieObject.ItemLabel.Direction.Length = LengthDictionary[pie.Category];
        }
        catch(Exception)
        {

        }
    }
}
