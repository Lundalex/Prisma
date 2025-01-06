using ChartAndGraph;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class colorRandomizer : MonoBehaviour
{
    public string category;
    public GraphChart chart;
    public Material baseMaterial;
    public GameObject textPrefab;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(SwitchColor()); 
    }

    IEnumerator SwitchColor()
    {
        while (true)
        {
            baseMaterial.color = Random.ColorHSV();
            chart.DataSource.SetCategoryPoint(category, baseMaterial, 5.0);
            var t = textPrefab.GetComponent<Text>();
            if(t != null)
            {
                t.color = Random.ColorHSV();
                var axis = chart.GetComponent<VerticalAxis>();
                if(axis != null)
                {
                    axis.MainDivisions.TextPrefab = t;
                    axis.SubDivisions.TextPrefab = t;
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
