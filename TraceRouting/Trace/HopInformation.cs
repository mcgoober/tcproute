using System.Collections.Generic;
using System.Linq;

namespace TraceRouting.Trace
{
    public class HopInformation
    {
        public HopInformation()
        {
            Results = new List<HopResult>();
        }

        public int Position { get; set; }

        public IList<HopResult> Results { get; set; }

        public int AverageLatency {
            get
            {
                return (int)Results.Select(x => x.Latency).Average();
            }
        }
    }
}
