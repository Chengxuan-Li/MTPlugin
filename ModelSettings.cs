using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTPlugin
{
    public class ModelSettings
    {
        public double OffsetDistance = 0.05;
        public double FootprintDiscretizationInterval = 5.0;
        public double LinkDiscretizationInterval = 10.0;
        public double JitterAmount = 0.0;
        public bool EnableAdaptiveNetwork = false;
    }
}
