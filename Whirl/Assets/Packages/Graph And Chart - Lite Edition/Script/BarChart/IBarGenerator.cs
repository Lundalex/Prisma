using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChartAndGraph
{
    public interface IBarGenerator
    {
        void Generate(float normalizedSize,float scale);
        void Clear();
    }
}
