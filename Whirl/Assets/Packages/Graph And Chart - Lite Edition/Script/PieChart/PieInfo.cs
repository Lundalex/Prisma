using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ChartAndGraph
{
    public class PieInfo : MonoBehaviour
    {
        public PieChart.PieObject pieObject { get; set; }
        public string Category { get { return pieObject.category; } }
    }
}
